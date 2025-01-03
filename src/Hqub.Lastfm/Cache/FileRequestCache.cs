﻿
namespace Hqub.Lastfm.Cache;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using CryptoMD5 = System.Security.Cryptography.MD5;

/// <summary>
/// Caches requests on disk.
/// </summary>
public class FileRequestCache : IRequestCache
{
    private const int HEADER_LENGTH = 512;

    /// <summary>
    /// Gets or sets the timeout for a cache entry to expire.
    /// </summary>
    public TimeSpan Timeout { get; set; }

    private readonly string path;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRequestCache"/> class.
    /// </summary>
    public FileRequestCache()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dimmer", "cache"))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRequestCache"/> class.
    /// </summary>
    /// <param name="path">The local path of the cache.</param>
    public FileRequestCache(string path)
    {
        Timeout = TimeSpan.FromHours(24.0);

        this.path = path;

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <inheritdoc />
    public async Task Add(string request, Stream response)
    {
        await CacheEntry.Write(path, request, response);
    }

    /// <inheritdoc />
    public Task<bool> TryGetCachedItem(string request, out Stream stream)
    {
        var item = CacheEntry.Read(path, request);

        stream = null;

        if (item == null)
        {
            return Task.FromResult(false);
        }

        if ((DateTime.Now - item.TimeStamp) > Timeout)
        {
            item.Stream.Close();

            return Task.FromResult(false);
        }
        
        stream = item.Stream;

        return Task.FromResult(true);
    }

    /// <summary>
    /// Remove all expired entries from the cache.
    /// </summary>
    /// <returns>The number of expired entries.</returns>
    public int Cleanup()
    {
        int count = 0;

        var now = DateTime.Now;

        foreach (var file in Directory.EnumerateFiles(path, "*.fm-cache"))
        {
            if ((now - CacheEntry.GetTimestamp(file)) > Timeout)
            {
                File.Delete(file);
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Remove all cache entries.
    /// </summary>
    public void Clear()
    {
        // If the path is used for cache only, we could just as well delete the directory.
        //if (Directory.Exists(path))
        //{
        //    Directory.Delete(path);
        //}

        foreach (var file in Directory.EnumerateFiles(path, "*.fm-cache"))
        {
            File.Delete(file);
        }
    }

    class CacheEntry
    {
        // Cache file header (512 bytes):
        //
        //      0  Timestamp (Int64 = 8 bytes)
        //      8  Request string (char*, max 504 bytes, null-terminated)

        public Stream Stream { get; private set; }

        public DateTime TimeStamp { get; private set; }

        public string Request { get; set; }

        public static CacheEntry Read(string path, string request)
        {
            const int REQUEST_LENGTH = HEADER_LENGTH - 8; // sizeof(long)

            // The byte buffer to hold the request string.
            var buffer = new byte[REQUEST_LENGTH];

            int size = Math.Min(request.Length, REQUEST_LENGTH);

            Encoding.UTF8.GetBytes(request, 0, size, buffer, 0);

            var file = GetCacheFileName(path, buffer, size);

            if (!File.Exists(file))
            {
                return null;
            }

            var stream = File.OpenRead(file);

            CacheEntry entry;

            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                entry = new CacheEntry();

                long timestamp = reader.ReadInt64();

                entry.TimeStamp = Utilities.TimestampToDateTime(timestamp, DateTimeKind.Utc);

                reader.Read(buffer, 0, REQUEST_LENGTH);

                size = 0;

                while (buffer[size++] != 0 && size < REQUEST_LENGTH) ;

                entry.Request = Encoding.UTF8.GetString(buffer, 0, size - 1);
            }

            // Check if the cached request matches the given (could be a hash collision).
            if (!request.Contains(entry.Request))
            {
                // Couldn't find content: invalidate the cache entry.
                entry.TimeStamp = DateTime.MinValue;
            }

            stream.Seek(HEADER_LENGTH, SeekOrigin.Begin);

            entry.Stream = stream;

            return entry;
        }

        public static async Task Write(string path, string request, Stream response)
        {
            const int REQUEST_LENGTH = HEADER_LENGTH - 8; // sizeof(long)

            // The byte buffer to hold the request string.
            var buffer = new byte[REQUEST_LENGTH];

            int size = Math.Min(request.Length, REQUEST_LENGTH);

            Encoding.UTF8.GetBytes(request, 0, size, buffer, 0);

            var name = GetCacheFileName(path, buffer, size);

            using (var stream = File.Create(Path.Combine(path, name)))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Utilities.DateTimeToUtcTimestamp(DateTime.Now));
                writer.Write(buffer);
                writer.Flush();

                await response.CopyToAsync(writer.BaseStream);
            }

            // Set the position of the response stream back to 0.
            response.Seek(0, SeekOrigin.Begin);
        }

        public static DateTime GetTimestamp(string file)
        {
            long timestamp = 0;

            using (var stream = File.OpenRead(file))
            using (var reader = new BinaryReader(stream))
            {
                timestamp = reader.ReadInt64();
            }

            return Utilities.TimestampToDateTime(timestamp, DateTimeKind.Utc);
        }

        private static string GetCacheFileName(string path, byte[] buffer, int size)
        {
            return Path.Combine(path, GetHash(buffer, size)) + ".fm-cache";
        }

        #region Helper methods

        /// <summary>
        /// Returns the hash of a string (based on MD5, but only 16 instead of 32 bytes).
        /// </summary>
        /// <param name="bytes">Input bytes.</param>
        /// <param name="count">The number of bytes to consider.</param>
        /// <returns>MD5 hash.</returns>
        private static string GetHash(byte[] bytes, int count)
        {
            var md5 = CryptoMD5.Create();

            bytes = md5.ComputeHash(bytes, 0, count);

            var buffer = new StringBuilder();

            for (int i = 0; i < bytes.Length; i += 2)
            {
                buffer.Append(bytes[i].ToString("x2").ToLower());
            }

            return buffer.ToString();
        }

        #endregion
    }
}
