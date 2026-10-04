using GWBASIC.Core.Common;

namespace GWBASIC.Core.Runtime;

public class BasicArray
{
    public string Name { get; }
    public BasicType ElementType { get; }
    public int[] LowerBounds { get; }
    public int[] UpperBounds { get; }
    private readonly BasicValue[] _storage;
    private readonly int[] _multipliers;

    public BasicArray(string name, BasicType type, int[] upperBounds, int optionBase)
    {
        Name = name;
        ElementType = type;
        int rank = upperBounds.Length;
        LowerBounds = new int[rank];
        UpperBounds = new int[rank];
        _multipliers = new int[rank];

        int totalElements = 1;
        for (int i = 0; i < rank; i++)
        {
            LowerBounds[i] = optionBase;
            UpperBounds[i] = upperBounds[i];
            int dimSize = UpperBounds[i] - LowerBounds[i] + 1;
            if (dimSize <= 0)
                throw new BasicException(BasicErrorCode.SubscriptOutOfRange);
            totalElements *= dimSize;
        }

        // Calculate multipliers for flat index
        int mul = 1;
        for (int i = rank - 1; i >= 0; i--)
        {
            _multipliers[i] = mul;
            mul *= (UpperBounds[i] - LowerBounds[i] + 1);
        }

        _storage = new BasicValue[totalElements];
        BasicValue defVal = type == BasicType.String ? BasicValue.EmptyString : BasicValue.Zero;
        Array.Fill(_storage, defVal);
    }

    public BasicValue GetElement(int[] indices)
    {
        int flatIndex = ComputeFlatIndex(indices);
        return _storage[flatIndex];
    }

    public void SetElement(int[] indices, BasicValue value)
    {
        int flatIndex = ComputeFlatIndex(indices);
        _storage[flatIndex] = value.ConvertTo(ElementType);
    }

    private int ComputeFlatIndex(int[] indices)
    {
        if (indices.Length != UpperBounds.Length)
            throw new BasicException(BasicErrorCode.SubscriptOutOfRange);

        int flat = 0;
        for (int i = 0; i < indices.Length; i++)
        {
            int idx = indices[i];
            if (idx < LowerBounds[i] || idx > UpperBounds[i])
                throw new BasicException(BasicErrorCode.SubscriptOutOfRange);
            flat += (idx - LowerBounds[i]) * _multipliers[i];
        }
        return flat;
    }
}
