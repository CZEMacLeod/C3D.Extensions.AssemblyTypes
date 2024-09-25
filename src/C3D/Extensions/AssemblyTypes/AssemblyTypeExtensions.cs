using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Reflection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class AssemblyTypeExtensions
{
    private static readonly ConcurrentDictionary<Assembly, Type[]> assemblyTypes = new();

    /// <summary>
    /// Gets an array of Types from an Assembly, supressing any errors that occur.
    /// </summary>
    /// <param name="assembly">An assembly to get the types from</param>
    /// <returns>An array of loadable types from the assembly</returns>
    /// <remarks>Because we may have exceptions and use try/catch in the internal method,
    /// we cache the result of the load such that multiple accesses do not incur the additional overhead.
    /// </remarks>
    public static Type[] GetLoadableTypes(this Assembly assembly) =>
        assembly.GetLoadableTypes(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

    /// <summary>
    /// Gets an array of Types from an Assembly, supressing and logging any errors that occur.
    /// </summary>
    /// <param name="assembly">An assembly to get the types from</param>
    /// <param name="logger">An ILogger used to log any load issues</param>
    /// <returns>An array of loadable types from the assembly</returns>
    /// <remarks>Because we may have exceptions and use try/catch in the internal method,
    /// we cache the result of the load such that multiple accesses do not incur the additional overhead.
    /// </remarks>
    public static Type[] GetLoadableTypes(this Assembly assembly, ILogger logger) =>
        assemblyTypes.GetOrAdd(assembly, GetLoadableTypesInternal, logger);

    private static Type[] GetLoadableTypesInternal(Assembly assembly, ILogger logger)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            using var scope = logger.BeginScope("Loading types from {assemblyName}", assembly.FullName);
            logger.LogWarning(ex, "Issue loading types from {assemblyName}", assembly.FullName);
            foreach (var le in ex.LoaderExceptions.Where(e => e is not null).OfType<Exception>())
            {
                if (le is TypeLoadException tle)
                {
                    logger.LogWarning(tle, "TypeLoadException for {typeName} in {assemblyName}", tle.TypeName, assembly.FullName);
                }
                else
                {
                    logger.LogWarning(le, "{exceptionType}: {message}", le.GetType().Name, le.Message);
                }
            }
            return ex.Types.Where(t => t is not null).ToArray()!;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load types from {assemblyName}", assembly.FullName);
            return [];
        }
    }
}