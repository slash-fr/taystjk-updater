using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TaystJkUpdater.Extensions
{
    // Source:  https://stackoverflow.com/a/46497896
    // License: CC BY-SA 3.0 / CC BY-SA 4.0 (possibly)
    // Authors: Bruno Zell and other StackOverflow contributors
    public static class StreamExtensions
    {
        /// <param name="progress">Reports the number of bytes copied so far</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static async Task CopyToAsync(
            this Stream source,
            Stream destination,
            int bufferSize,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!source.CanRead)
            {
                throw new ArgumentException("The source has to be readable", nameof(source));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (!destination.CanWrite)
            {
                throw new ArgumentException("The destination has to be writable", nameof(destination));
            }

            if (bufferSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            byte[] buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }
        }
    }
}
