namespace Back_End.DTOs
{
    public record PlayerPosition(
    string PlayerId,
    float X,
    float Y,
    float Z,
    float RotationY);
}
