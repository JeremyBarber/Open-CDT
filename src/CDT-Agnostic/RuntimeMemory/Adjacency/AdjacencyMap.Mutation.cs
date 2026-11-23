using System.Numerics;
using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Adjacency;

internal unsafe partial struct AdjacencyMap
{
    /// <summary>
    /// Appends <paramref name="id"/> to the tail block of <paramref name="entityId"/> in strictly <c>O(1)</c> time,
    /// renting and linking a new 64-byte <see cref="AdjacencyBlock"/> if the current tail block is full.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(int entityId, int id)
    {
        ref var head = ref _entityHeads[entityId];
        ref var tail = ref _entityTails[entityId];

        if (head == InvalidData)
            head = tail = RentBlock();

        var last = &_pool[tail];

        if (last->Count == AdjacencyBlock.Capacity)
        {
            var newIdx = RentBlock();
            last->Next = newIdx;
            last = &_pool[newIdx];

            tail = newIdx;
        }

        last->Ids[last->Count++] = id;
    }

    /// <summary>
    /// Removes <paramref name="id"/> from the adjacency chain of <paramref name="entityId"/> via cross-page swap-and-pop,
    /// keeping all pages densely packed and returning the tail block to the pool if it empties.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Remove(int entityId, int id)
    {
        ref var head = ref _entityHeads[entityId];
        if (head == InvalidData)
            return;

        var headPage = &_pool[head];

        if (headPage->Next == InvalidData)
            RemoveSinglePage(ref head, ref _entityTails[entityId], headPage, id);
        else
            RemoveMultiPage(ref head, ref _entityTails[entityId], id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveSinglePage(ref int head, ref int tail, AdjacencyBlock* page, int id)
    {
        var mask = AdjacencyBlock.GetMatchMask(page->Ids, page->Count, id);
        if (mask == 0)
            return;

        var i = BitOperations.TrailingZeroCount(mask);
        page->Ids[i] = page->Ids[--page->Count];

        if (page->Count == 0)
        {
            ReturnBlock(head);
            head = tail = InvalidData;
        }
    }

    private void RemoveMultiPage(ref int head, ref int tail, int id)
    {
        var blockIdx = head;

        while (blockIdx != InvalidData)
        {
            var page = &_pool[blockIdx];
            var mask = AdjacencyBlock.GetMatchMask(page->Ids, page->Count, id);

            if (mask != 0)
            {
                var i = BitOperations.TrailingZeroCount(mask);

                // Cross-page swap-and-pop: overwrite the deleted slot with the last element of the tail page.
                // This guarantees every intermediate page in the chain remains 100% full (Count == 14).
                var tailPage = &_pool[tail];
                page->Ids[i] = tailPage->Ids[--tailPage->Count];

                if (tailPage->Count > 0)
                    return; // ~93% of removals exit immediately here without pointer-chasing

                ReturnBlock(tail);

                // The tail block just dropped to 0 items: walk from head to find the block
                // immediately preceding tail so we can unlink tail and re-anchor the tail index.
                var walkIdx = head;
                var walkPage = &_pool[walkIdx];

                while (walkPage->Next != tail)
                {
                    walkIdx = walkPage->Next;
                    walkPage = &_pool[walkIdx];
                }

                walkPage->Next = InvalidData;
                tail = walkIdx;

                return;
            }

            blockIdx = page->Next;
        }
    }
}