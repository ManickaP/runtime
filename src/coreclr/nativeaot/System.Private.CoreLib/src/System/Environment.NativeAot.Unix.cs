// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime;
using System.Threading;

namespace System
{
    public static partial class Environment
    {
        [DoesNotReturn]
        private static void ExitRaw() => Interop.Sys.Exit(s_latchedExitCode);
    }
}
