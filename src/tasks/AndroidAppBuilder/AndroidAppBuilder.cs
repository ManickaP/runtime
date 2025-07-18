﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

public class AndroidAppBuilderTask : Task
{
    [Required]
    public string[] RuntimeHeaders { get; set; } = [];

    /// <summary>
    /// Target directory with *dll and other content to be AOT'd and/or bundled
    /// </summary>
    [Required]
    public string AppDir { get; set; } = ""!;

    /// <summary>
    /// This library will be used as an entry-point (e.g. TestRunner.dll)
    /// </summary>
    public string MainLibraryFileName { get; set; } = ""!;

    /// <summary>
    /// List of paths to assemblies to be included in the app. For AOT builds the 'ObjectFile' metadata key needs to point to the object file.
    /// </summary>
    public ITaskItem[] Assemblies { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// The set of environment variables to provide to the native embedded application
    /// </summary>
    public ITaskItem[] EnvironmentVariables { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// Additional linker arguments that apply to the app being built
    /// </summary>
    public ITaskItem[] ExtraLinkerArguments { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// Prefer FullAOT mode for Emulator over JIT
    /// </summary>
    public bool ForceAOT { get; set; }

    /// <summary>
    /// Indicates if we want to AOT all assemblies or not
    /// </summary>
    public bool ForceFullAOT { get; set; }

    /// <summary>
    /// Mode to control whether runtime is a self-contained library or not
    /// </summary>
    public bool IsLibraryMode { get; set; }

    /// <summary>
    /// Extra native dependencies to link into the app
    /// </summary>
    public string[] NativeDependencies { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Static linked runtime
    /// </summary>
    public bool StaticLinkedRuntime { get; set; }

    /// <summary>
    /// List of enabled runtime components
    /// </summary>
    public string[] RuntimeComponents { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Diagnostic ports configuration string
    /// </summary>
    public string? DiagnosticPorts { get; set; } = ""!;

    [Required]
    public string RuntimeIdentifier { get; set; } = ""!;

    [Required]
    public string OutputDir { get; set; } = ""!;

    [Required]
    public string? ProjectName { get; set; }

    public string? AndroidSdk { get; set; }

    public string? AndroidNdk { get; set; }

    public string? MinApiLevel { get; set; }

    public string? TargetApiLevel { get; set; }

    public string? BuildApiLevel { get; set; }

    public string? BuildToolsVersion { get; set; }

    public bool StripDebugSymbols { get; set; }

    public string RuntimeFlavor { get; set; } = nameof(RuntimeFlavorEnum.Mono);

    /// <summary>
    /// Path to a custom MainActivity.java with custom UI
    /// A default one is used if it's not set
    /// </summary>
    public string? NativeMainSource { get; set; }

    public string? KeyStorePath { get; set; }

    public bool ForceInterpreter { get; set; }

    /// <summary>
    /// Indicates whether we want to use invariant globalization mode.
    /// </summary>
    public bool InvariantGlobalization { get; set; }

    [Output]
    public string ApkBundlePath { get; set; } = ""!;

    [Output]
    public string ApkPackageId { get; set; } = ""!;

    public override bool Execute()
    {
        var apkBuilder = new ApkBuilder(Log);
        apkBuilder.ProjectName = ProjectName;
        apkBuilder.AppDir = AppDir;
        apkBuilder.OutputDir = OutputDir;
        apkBuilder.AndroidSdk = AndroidSdk;
        apkBuilder.AndroidNdk = AndroidNdk;
        apkBuilder.MinApiLevel = MinApiLevel;
        apkBuilder.TargetApiLevel = TargetApiLevel;
        apkBuilder.BuildApiLevel = BuildApiLevel;
        apkBuilder.BuildToolsVersion = BuildToolsVersion;
        apkBuilder.StripDebugSymbols = StripDebugSymbols;
        apkBuilder.NativeMainSource = NativeMainSource;
        apkBuilder.KeyStorePath = KeyStorePath;
        apkBuilder.ForceInterpreter = ForceInterpreter;
        apkBuilder.ForceAOT = ForceAOT;
        apkBuilder.ForceFullAOT = ForceFullAOT;
        apkBuilder.EnvironmentVariables = EnvironmentVariables;
        apkBuilder.StaticLinkedRuntime = StaticLinkedRuntime;
        apkBuilder.RuntimeComponents = RuntimeComponents;
        apkBuilder.DiagnosticPorts = DiagnosticPorts;
        apkBuilder.Assemblies = Assemblies;
        apkBuilder.IsLibraryMode = IsLibraryMode;
        apkBuilder.NativeDependencies = NativeDependencies;
        apkBuilder.ExtraLinkerArguments = ExtraLinkerArguments;
        apkBuilder.RuntimeFlavor = RuntimeFlavor;
        apkBuilder.InvariantGlobalization = InvariantGlobalization;
        (ApkBundlePath, ApkPackageId) = apkBuilder.BuildApk(RuntimeIdentifier, MainLibraryFileName, RuntimeHeaders);

        return true;
    }
}
