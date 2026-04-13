// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

/// <summary>
/// Vendored from dotnet/runtime as part of the regex semantic front-end pipeline.
/// This is the structural node-kind enum used by the runtime parser/tree pipeline.
/// </summary>
internal enum RegexNodeKind : byte
{
    Unknown = 0,
    One = RegexOpcode.One,
    Notone = RegexOpcode.Notone,
    Set = RegexOpcode.Set,
    Multi = RegexOpcode.Multi,
    Oneloop = RegexOpcode.Oneloop,
    Notoneloop = RegexOpcode.Notoneloop,
    Setloop = RegexOpcode.Setloop,
    Onelazy = RegexOpcode.Onelazy,
    Notonelazy = RegexOpcode.Notonelazy,
    Setlazy = RegexOpcode.Setlazy,
    Oneloopatomic = RegexOpcode.Oneloopatomic,
    Notoneloopatomic = RegexOpcode.Notoneloopatomic,
    Setloopatomic = RegexOpcode.Setloopatomic,
    Backreference = RegexOpcode.Backreference,
    Bol = RegexOpcode.Bol,
    Eol = RegexOpcode.Eol,
    Boundary = RegexOpcode.Boundary,
    NonBoundary = RegexOpcode.NonBoundary,
    ECMABoundary = RegexOpcode.ECMABoundary,
    NonECMABoundary = RegexOpcode.NonECMABoundary,
    Beginning = RegexOpcode.Beginning,
    Start = RegexOpcode.Start,
    EndZ = RegexOpcode.EndZ,
    End = RegexOpcode.End,
    UpdateBumpalong = RegexOpcode.UpdateBumpalong,
    Nothing = RegexOpcode.Nothing,
    Empty = 23,
    Alternate = 24,
    Concatenate = 25,
    Loop = 26,
    Lazyloop = 27,
    Capture = 28,
    Group = 29,
    PositiveLookaround = 30,
    NegativeLookaround = 31,
    Atomic = 32,
    BackreferenceConditional = 33,
    ExpressionConditional = 34,
}
