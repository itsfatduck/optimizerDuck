namespace optimizerDuck.Common.Converters;

/// <summary>Inverts a <see cref="bool"/>: <c>true</c> becomes <c>false</c> and vice versa.</summary>
public sealed class InverseBooleanConverter() : BooleanConverter<bool>(false, true);
