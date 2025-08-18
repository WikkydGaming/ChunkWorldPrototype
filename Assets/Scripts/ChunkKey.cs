using System;

[Serializable]
public struct ChunkKey : IEquatable<ChunkKey>
{
    public int cx, cy;
    public ChunkKey(int cx, int cy) { this.cx = cx; this.cy = cy; }
    public bool Equals(ChunkKey o) => cx == o.cx && cy == o.cy;
    public override bool Equals(object obj) => obj is ChunkKey o && Equals(o);
    public override int GetHashCode() => (cx * 73856093) ^ (cy * 19349663);
    public override string ToString() => $"({cx},{cy})";
}
