# Lokad.Utf8Regex.Posix

The optional POSIX ERE adapter for .NET 10/11 is under development for 0.5.0.
Its [contract](../../docs/SPEC-POSIX.md) defines strict UTF-8 scalar matching,
leftmost-longest selection and capture priority. It is not available in 0.4.0
or the qualified three-package dev.55 candidate.

The adapter depends only on Core. Execution will remain fully managed and checked,
without a native or BCL regex fallback.
