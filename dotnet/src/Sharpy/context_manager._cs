using System;

public interface IContextManager
{
    public IDisposable EnterContext();
    public void ExitContext(Exception exception);
}

public static class WithContext
{
    public static void Execute<T>(IContextManager contextManager, Action<T> body)
    {
        using (IDisposable managed = contextManager.EnterContext())
        {
            try
            {
                body(managed);
            }
            catch (Exception exception)
            {
                contextManager.ExitContext(exception);
            }
            finally
            {
                contextManager.ExitContext(null);
            }
        }
    }

    static T? Execute<T>(IContextManager contextManager, Func<T> block)
    {
        using (IContextManaged managed = contextManager.EnterContext())
        {
            try
            {
                return block();
            }
            catch (Exception ex)
            {
                managed.Handle(ex);
            }
        }

        return null;
    }
}
