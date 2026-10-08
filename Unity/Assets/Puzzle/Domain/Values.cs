using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DeclarativePuzzle.Domain
{
    /// <summary>A defensively copied, structurally comparable collection.</summary>
    public sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
    {
        readonly T[] items;
        public static ValueList<T> Empty { get; } = new ValueList<T>(Array.Empty<T>());
        public ValueList(IEnumerable<T> items) => this.items = items.ToArray();
        public int Count => items.Length;
        public T this[int index] => items[index];
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool Equals(ValueList<T> other) => other != null && items.SequenceEqual(other.items);
        public override bool Equals(object obj) => obj is ValueList<T> other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                foreach (var item in items) hash = hash * 31 + EqualityComparer<T>.Default.GetHashCode(item);
                return hash;
            }
        }
    }

    public readonly struct Cell : IEquatable<Cell>
    {
        public int X { get; }
        public int Y { get; }
        public Cell(int x, int y) { X = x; Y = y; }
        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public override string ToString() => $"({X}, {Y})";
    }

    public enum QuarterTurn { North, East, South, West }
    public enum RotationDirection { CounterClockwise = -1, Clockwise = 1 }
    public enum SubmissionPhase { Playing, Submitting, Celebrating, Finished }

    public sealed record PieceDefinition
    {
        public string Id { get; }
        public ValueList<Cell> Cells { get; }
        public PieceDefinition(string id, IEnumerable<Cell> cells)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A piece needs an ID.");
            var source = cells.ToArray();
            if (source.Length == 0 || source.Distinct().Count() != source.Length)
                throw new ArgumentException($"{id}: cells must be nonempty and unique.");
            var connected = new HashSet<Cell> { source[0] };
            var queue = new Queue<Cell>();
            queue.Enqueue(source[0]);
            var all = new HashSet<Cell>(source);
            var neighbors = new[] { new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1) };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var delta in neighbors)
                    if (all.Contains(cell + delta) && connected.Add(cell + delta)) queue.Enqueue(cell + delta);
            }
            if (connected.Count != source.Length) throw new ArgumentException($"{id}: polyomino must be connected.");
            var minX = source.Min(c => c.X);
            var minY = source.Min(c => c.Y);
            Id = id;
            Cells = new ValueList<Cell>(source.Select(c => new Cell(c.X - minX, c.Y - minY))
                .OrderBy(c => c.Y).ThenBy(c => c.X));
        }
    }

    public sealed record PuzzleDefinition
    {
        public int Width { get; }
        public int Height { get; }
        public ValueList<PieceDefinition> Pieces { get; }
        public PuzzleDefinition(int width, int height, IEnumerable<PieceDefinition> pieces)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Board dimensions must be positive.");
            Width = width;
            Height = height;
            Pieces = new ValueList<PieceDefinition>(pieces);
            if (Pieces.Count == 0 || Pieces.Any(p => p == null) || Pieces.Select(p => p.Id).Distinct().Count() != Pieces.Count)
                throw new ArgumentException("Pieces need unique IDs.");
            if (Pieces.Sum(p => p.Cells.Count) != width * height)
                throw new ArgumentException("Total piece area must equal board area.");
            foreach (var piece in Pieces)
                if (!Enum.GetValues(typeof(QuarterTurn)).Cast<QuarterTurn>().Any(r =>
                    GameRules.Shape(piece, r).All(c => c.X < width && c.Y < height)))
                    throw new ArgumentException($"{piece.Id}: no orientation fits this board.");
        }
        public PieceDefinition Piece(string id) => Pieces.First(p => p.Id == id);
    }
}
