﻿namespace WireGuardCommand.Extensions;

public static class ByteExtensions
{
    public static string ToBase64(this byte[] bytes)
    {
        return Convert.ToBase64String(bytes);
    }
}
