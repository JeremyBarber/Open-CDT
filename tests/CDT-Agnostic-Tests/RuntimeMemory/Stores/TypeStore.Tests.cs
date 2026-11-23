using CDT_Agnostic.RandomNumberGeneration;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using CDT_Agnostic.RuntimeMemory.Stores;

using Xunit;

namespace CDT_Agnostic_Tests.RuntimeMemory.Stores;

public sealed class TypeStoreTests
{
    [Fact]
    public void AddAndRemove_WithFragmentedIds_MaintainsDenseSpanAndInversePositions()
    {
        var store = new TypeStore(maxIds: 100);
        try
        {
            Assert.Equal(0, store.Count);
            Assert.True(store.IdsOfType.IsEmpty);

            // 1. Add fragmented IDs: _active = [0, 15, 42, 99]
            store.Add(new SimplexId(0));
            store.Add(new SimplexId(15));
            store.Add(new SimplexId(42));
            store.Add(new SimplexId(99));

            Assert.Equal(4, store.Count);
            Assert.Equal([0, 15, 42, 99], store.IdsOfType.ToArray());

            // 2. Remove interior ID 15 (at pos 1) -> tail ID 99 moves into pos 1: _active = [0, 99, 42]
            store.Remove(new SimplexId(15));
            Assert.Equal(3, store.Count);
            Assert.Equal([0, 99, 42], store.IdsOfType.ToArray());

            // 3. Remove ID 99 (which was displaced to pos 1!) -> tail ID 42 moves into pos 1: _active = [0, 42]
            store.Remove(new SimplexId(99));
            Assert.Equal(2, store.Count);
            Assert.Equal([0, 42], store.IdsOfType.ToArray());

            // 4. Remove the current tail element directly (ID 42 at pos 1, where id.Value == last): _active = [0]
            store.Remove(new SimplexId(42));
            Assert.Equal(1, store.Count);
            Assert.Equal([0], store.IdsOfType.ToArray());

            // 5. Re-adding a previously removed ID works cleanly
            store.Add(new SimplexId(15));
            Assert.Equal(2, store.Count);
            Assert.Equal([0, 15], store.IdsOfType.ToArray());
        }
        finally
        {
            store.Dispose();
        }
    }

    [Fact]
    public void SelectRandom_DrawsExclusivelyFromRegisteredActiveSet()
    {
        var store = new TypeStore(maxIds: 100);
        try
        {
            store.Add(new SimplexId(10));
            store.Add(new SimplexId(50));
            store.Add(new SimplexId(90));

            Pcg32 rng = default;
            rng.Seed(initState: 42UL, initInc: 54UL);

            bool saw10 = false, saw50 = false, saw90 = false;

            for (var i = 0; i < 64; i++)
            {
                var drawn = store.SelectRandom(ref rng);
                Assert.True(drawn.Value is 10 or 50 or 90);

                saw10 |= drawn.Value == 10;
                saw50 |= drawn.Value == 50;
                saw90 |= drawn.Value == 90;
            }

            Assert.True(saw10 && saw50 && saw90);
        }
        finally
        {
            store.Dispose();
        }
    }
}