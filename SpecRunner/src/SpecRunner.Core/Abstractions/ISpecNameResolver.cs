namespace SpecRunner.Core.Abstractions;

public interface ISpecNameResolver
{
    string Resolve(int issueNumber, string issueTitle);
}
