namespace SolmangoNET;

public static class SolmangoExtensions
{
    public static double ToSOL(this ulong amount) => amount / 1_000_000_000D;

    public static ulong ToLamports(this ulong amount) => amount * 1_000_000_000;
}