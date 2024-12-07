﻿namespace SentinelCore.Domain.Utils.Extensions
{
    public static class DirectoryExtensions
    {
        public static void EnsureDirExistence(this string value)
        {
            if (!Directory.Exists(value))
            {
                Directory.CreateDirectory(value);
            }
        }
    }
}
