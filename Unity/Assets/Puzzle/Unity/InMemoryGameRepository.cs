using System;
using DeclarativePuzzle.Application;
using DeclarativePuzzle.Domain;

namespace DeclarativePuzzle.Unity
{
    public sealed class InMemoryGameRepository : IGameRepository
    {
        GameState state;
        public InMemoryGameRepository(GameState initialState) => state = initialState;
        public GameState Load() => state;
        public void Save(GameState next) => state = next ?? throw new ArgumentNullException(nameof(next));
    }
}
