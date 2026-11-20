namespace Agent.Core.Services
{
    /// <summary>Semi-transparent accent color computed for the notification banner background.</summary>
    public struct AccentBackground
    {
        public AccentBackground(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public byte A { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
    }

    /// <summary>
    /// UI-free color math behind the notification banner's semi-transparent accent background.
    /// The WPF layer turns the returned channels back into a frozen <c>SolidColorBrush</c>.
    /// </summary>
    public static class AccentBackgroundCalculator
    {
        /// <summary>Alpha channel for the banner background tint (~20% opacity).</summary>
        public const byte Alpha = 0x33;

        public const byte FallbackR = 0x64;
        public const byte FallbackG = 0xB5;
        public const byte FallbackB = 0xF6;

        /// <summary>Builds the semi-transparent version of a source accent color.</summary>
        public static AccentBackground FromColor(byte r, byte g, byte b)
            => new AccentBackground(Alpha, r, g, b);

        /// <summary>Fallback accent background used when the source brush is not a solid color.</summary>
        public static AccentBackground Fallback()
            => new AccentBackground(Alpha, FallbackR, FallbackG, FallbackB);
    }
}
