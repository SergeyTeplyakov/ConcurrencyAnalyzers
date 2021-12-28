namespace ConcurrencyAnalyzers;

public class Unit
{
    private Unit() {}

    public static Unit Void { get; } = new Unit();

    public static Result<Unit> VoidSuccess { get; } = Result.Success(Void);
}