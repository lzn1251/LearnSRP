using System.Runtime.InteropServices;

public static class ReinterpretExtensions
{
    // The Light.renderingLayerMask property exposes its bit mask as an int
    //   and it gets garbled during the conversion to float in the light setup methods.
    // There is no way to directly send an array of integers to the GPU,
    //   so we have to somehow reinterpret an int as a float without conversion
    
    // C# is strongly typed

    [StructLayout(LayoutKind.Explicit)]
    struct IntFloat
    {
        [FieldOffset(0)]
        public int intValue;

        [FieldOffset(0)]
        public float floatValue;
    }
    
    public static float ReinterpretAsFloat(this int value)
    {
        IntFloat converter = default;
        converter.intValue = value;
        return converter.floatValue;
    }
}