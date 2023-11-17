namespace Blurhash
{
    /// <summary>
    /// Represents a pixel within the Blurhash algorithm
    /// </summary>
    public struct Pixel
    {
        public float Red;
        public float Green;
        public float Blue;

        public Pixel(float red, float green, float blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }
    }
}
