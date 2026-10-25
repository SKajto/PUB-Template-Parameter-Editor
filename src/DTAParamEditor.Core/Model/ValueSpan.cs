namespace DTAParamEditor.Core.Model;

/// <summary>
/// Exact character span of a logical value inside the decoded template XML text.
/// For attributes this excludes the surrounding quotes; for description text it is
/// the raw text content between the expression element boundaries.
/// Byte positions are derived later from the known encoding; char offsets are the
/// authoritative parse-time anchor.
/// </summary>
public readonly record struct ValueSpan(
    int StartChar,
    int LengthChar,
    int Line,
    int LinePosition)
{
    public bool IsEmpty => LengthChar == 0;
}
