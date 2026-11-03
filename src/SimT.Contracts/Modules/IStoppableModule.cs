namespace SimT.Contracts.Modules;

/// <summary>
/// Optional second lifecycle hook. Modules that hold external resources (files, native
/// handles) implement it; the kernel calls <see cref="Stop"/> in reverse start order.
/// </summary>
public interface IStoppableModule
{
    void Stop();
}
