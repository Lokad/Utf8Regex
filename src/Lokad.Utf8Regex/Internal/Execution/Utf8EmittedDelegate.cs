
namespace Lokad.Utf8Regex.Internal.Execution;

// Each builder publishes one delegate, retaining three final CLR-managed copies.
// The receipt is final retained capacity, not a construction high-water mark.
// Its final allowance survives builder disposal; cache certificates may charge
// aliased storage again instead of tracking delegate identities.
internal readonly record struct Utf8EmittedDelegate<TDelegate>(TDelegate Callback, ulong ManagedArrayBytes)
    where TDelegate : Delegate;
