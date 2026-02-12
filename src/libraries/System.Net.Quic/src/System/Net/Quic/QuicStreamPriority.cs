// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Quic;

/// <summary>
/// Holds the constants for <see cref="QuicStream.Priority"/>.
/// </summary>
/// <seealso href="https://www.rfc-editor.org/rfc/rfc9000.html#name-stream-prioritization" />
public enum QuicStreamPriority : byte
{
    /// <summary>
    /// The lowest possible value for the priority, stream data will be sent with least priority.
    /// </summary>
    Lowest = 0x00,

    /// <summary>
    /// The default value for the priority, priority of the stream data will not be changed.
    /// </summary>
    Default = 0x7F,

    /// <summary>
    /// The highest possible value for the priority, stream data will be sent with most priority.
    /// </summary>
    Highest = 0xFF,
}
