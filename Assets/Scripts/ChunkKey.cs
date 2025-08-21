using System;

[Serializable]
public struct ChunkKey : IEquatable<ChunkKey>
{
    public int cx, cz;
    public ChunkKey(int cx, int cy) { this.cx = cx; this.cz = cy; }
    public bool Equals(ChunkKey o) => cx == o.cx && cz == o.cz;
    public override bool Equals(object obj) => obj is ChunkKey o && Equals(o);
    public override int GetHashCode() => (cx * 73856093) ^ (cz * 19349663);
    public override string ToString() => $"({cx},{cz})";
}
