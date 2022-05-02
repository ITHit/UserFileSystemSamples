using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Stream helper functions.
    /// </summary>
    public static class StreamCopy
    {

        /// <summary>
        /// Asynchronously copies specified number of bytes from current stream to destination stream, using a specified buffer size.
        /// </summary>
        /// <param name="source">Source stream.</param>
        /// <param name="destination">The stream to which the contents of the current file stream will be copied.</param>
        /// <param name="bufferSize">The size, in bytes, of the buffer. This value must be greater than zero.</param>
        /// <param name="count">Number of bytes to copy.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, long count, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[bufferSize];
            int read;
            while (count > 0 
                && (read = await source.ReadAsync(buffer, 0, (int)Math.Min(buffer.LongLength, count), cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer, 0, read, cancellationToken);
                count -= read;
            }
        }
    }
}
