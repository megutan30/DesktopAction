// Core/DependencyInjection/IServiceContainer.cs
using System.Reflection;

namespace MultiWindowActionGame.Core.DependencyInjection
{
    /// <summary>
    /// サービスコンテナのインターフェース
    /// </summary>
    public interface IServiceContainer : IDisposable
    {
        // サービス登録
        IServiceContainer RegisterSingleton<TInterface, TImplementation>()
            where TImplementation : class, TInterface;
        
        IServiceContainer RegisterSingleton<TInterface>(TInterface instance)
            where TInterface : class;
        
        IServiceContainer RegisterSingleton<TInterface>(Func<IServiceContainer, TInterface> factory)
            where TInterface : class;
        
        IServiceContainer RegisterTransient<TInterface, TImplementation>()
            where TImplementation : class, TInterface;
        
        IServiceContainer RegisterTransient<TInterface>(Func<IServiceContainer, TInterface> factory)
            where TInterface : class;
        
        IServiceContainer RegisterScoped<TInterface, TImplementation>()
            where TImplementation : class, TInterface;

        // サービス解決
        TInterface GetService<TInterface>();
        TInterface GetRequiredService<TInterface>();
        object GetService(Type serviceType);
        object GetRequiredService(Type serviceType);
        
        // サービス情報
        bool IsRegistered<TInterface>();
        bool IsRegistered(Type serviceType);
        IEnumerable<Type> GetRegisteredServices();
        
        // スコープ管理
        IServiceScope CreateScope();
    }

    /// <summary>
    /// サービススコープ
    /// </summary>
    public interface IServiceScope : IDisposable
    {
        IServiceContainer ServiceProvider { get; }
    }

    /// <summary>
    /// サービスの生存期間
    /// </summary>
    public enum ServiceLifetime
    {
        Singleton,
        Transient,
        Scoped
    }
}

// Core/DependencyInjection/ServiceDescriptor.cs
namespace MultiWindowActionGame.Core.DependencyInjection
{
    /// <summary>
    /// サービス登録情報
    /// </summary>
    internal class ServiceDescriptor
    {
        public Type ServiceType { get; set; } = null!;
        public Type? ImplementationType { get; set; }
        public object? Instance { get; set; }
        public Func<IServiceContainer, object>? Factory { get; set; }
        public ServiceLifetime Lifetime { get; set; }

        public bool IsValid => 
            ServiceType != null && 
            (ImplementationType != null || Instance != null || Factory != null);
    }
}

// Core/DependencyInjection/SimpleServiceContainer.cs
namespace MultiWindowActionGame.Core.DependencyInjection
{
    /// <summary>
    /// シンプルなサービスコンテナの実装
    /// </summary>
    public class SimpleServiceContainer : IServiceContainer
    {
        private readonly Dictionary<Type, ServiceDescriptor> _services = new();
        private readonly Dictionary<Type, object> _singletonInstances = new();
        private readonly Dictionary<Type, object> _scopedInstances = new();
        private readonly object _lock = new();
        private bool _disposed = false;

        public IServiceContainer RegisterSingleton<TInterface, TImplementation>()
            where TImplementation : class, TInterface
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                _services[typeof(TInterface)] = new ServiceDescriptor
                {
                    ServiceType = typeof(TInterface),
                    ImplementationType = typeof(TImplementation),
                    Lifetime = ServiceLifetime.Singleton
                };
            }
            return this;
        }

        public IServiceContainer RegisterSingleton<TInterface>(TInterface instance)
            where TInterface : class
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(instance);
            
            lock (_lock)
            {
                _services[typeof(TInterface)] = new ServiceDescriptor
                {
                    ServiceType = typeof(TInterface),
                    Instance = instance,
                    Lifetime = ServiceLifetime.Singleton
                };
                _singletonInstances[typeof(TInterface)] = instance;
            }
            return this;
        }

        public IServiceContainer RegisterSingleton<TInterface>(Func<IServiceContainer, TInterface> factory)
            where TInterface : class
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(factory);
            
            lock (_lock)
            {
                _services[typeof(TInterface)] = new ServiceDescriptor
                {
                    ServiceType = typeof(TInterface),
                    Factory = container => factory(container),
                    Lifetime = ServiceLifetime.Singleton
                };
            }
            return this;
        }

        public IServiceContainer RegisterTransient<TInterface, TImplementation>()
            where TImplementation : class, TInterface
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                _services[typeof(TInterface)] = new ServiceDescriptor
                {
                    ServiceType = typeof(TInterface),
                    ImplementationType = typeof(TImplementation),
                    Lifetime = ServiceLifetime.Transient
                };
            }
            return this;
        }

        public IServiceContainer RegisterTransient<TInterface>(Func<IServiceContainer, TInterface> factory)
            where TInterface : class
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(factory);
            
            lock (_lock)
            {
                _services[typeof(TInterface)] = new ServiceDescriptor
                {
                    ServiceType = typeof(TInterface),
                    Factory = container => factory(container),
                    Lifetime = ServiceLifetime.Transient
                };
            }
            return this;
        }

        public IServiceContainer RegisterScoped<TInterface, TImplementation>()
            where TImplementation : class, TInterface
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                _services[typeof(TInterface)] = new ServiceDescriptor
                {
                    ServiceType = typeof(TInterface),
                    ImplementationType = typeof(TImplementation),
                    Lifetime = ServiceLifetime.Scoped
                };
            }
            return this;
        }

        public TInterface GetService<TInterface>()
        {
            var service = GetService(typeof(TInterface));
            return service == null ? default! : (TInterface)service;
        }

        public TInterface GetRequiredService<TInterface>()
        {
            var service = GetService<TInterface>();
            if (service == null)
            {
                throw new InvalidOperationException($"Service of type {typeof(TInterface).Name} is not registered.");
            }
            return service;
        }

        public object GetService(Type serviceType)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(serviceType);
            
            lock (_lock)
            {
                if (!_services.TryGetValue(serviceType, out var descriptor))
                {
                    return null!;
                }

                return descriptor.Lifetime switch
                {
                    ServiceLifetime.Singleton => GetSingletonInstance(descriptor),
                    ServiceLifetime.Transient => CreateInstance(descriptor),
                    ServiceLifetime.Scoped => GetScopedInstance(descriptor),
                    _ => throw new ArgumentOutOfRangeException($"Unknown service lifetime: {descriptor.Lifetime}")
                };
            }
        }

        public object GetRequiredService(Type serviceType)
        {
            var service = GetService(serviceType);
            if (service == null)
            {
                throw new InvalidOperationException($"Service of type {serviceType.Name} is not registered.");
            }
            return service;
        }

        public bool IsRegistered<TInterface>()
        {
            return IsRegistered(typeof(TInterface));
        }

        public bool IsRegistered(Type serviceType)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(serviceType);
            
            lock (_lock)
            {
                return _services.ContainsKey(serviceType);
            }
        }

        public IEnumerable<Type> GetRegisteredServices()
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                return _services.Keys.ToList();
            }
        }

        public IServiceScope CreateScope()
        {
            ThrowIfDisposed();
            return new ServiceScope(this);
        }

        private object GetSingletonInstance(ServiceDescriptor descriptor)
        {
            if (_singletonInstances.TryGetValue(descriptor.ServiceType, out var instance))
            {
                return instance;
            }

            instance = descriptor.Instance ?? CreateInstance(descriptor);
            _singletonInstances[descriptor.ServiceType] = instance;
            return instance;
        }

        private object GetScopedInstance(ServiceDescriptor descriptor)
        {
            if (_scopedInstances.TryGetValue(descriptor.ServiceType, out var instance))
            {
                return instance;
            }

            instance = CreateInstance(descriptor);
            _scopedInstances[descriptor.ServiceType] = instance;
            return instance;
        }

        private object CreateInstance(ServiceDescriptor descriptor)
        {
            try
            {
                if (descriptor.Factory != null)
                {
                    return descriptor.Factory(this);
                }

                if (descriptor.ImplementationType == null)
                {
                    throw new InvalidOperationException($"No implementation type registered for {descriptor.ServiceType.Name}");
                }

                return CreateInstanceWithDependencyInjection(descriptor.ImplementationType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create instance of {descriptor.ServiceType.Name}: {ex.Message}", ex);
            }
        }

        private object CreateInstanceWithDependencyInjection(Type implementationType)
        {
            var constructors = implementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            
            // パラメータ数が最も多いコンストラクタを選択（依存性注入用）
            var constructor = constructors
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (constructor == null)
            {
                throw new InvalidOperationException($"No public constructor found for {implementationType.Name}");
            }

            var parameters = constructor.GetParameters();
            if (parameters.Length == 0)
            {
                return Activator.CreateInstance(implementationType)!;
            }

            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;
                var service = GetService(parameterType);
                
                if (service == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot resolve service for type {parameterType.Name} " +
                        $"required by constructor of {implementationType.Name}");
                }
                
                args[i] = service;
            }

            return Activator.CreateInstance(implementationType, args)!;
        }

        internal void ClearScopedInstances()
        {
            lock (_lock)
            {
                foreach (var instance in _scopedInstances.Values)
                {
                    if (instance is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error disposing scoped service: {ex.Message}");
                        }
                    }
                }
                _scopedInstances.Clear();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SimpleServiceContainer));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                // スコープ付きインスタンスを破棄
                ClearScopedInstances();

                // シングルトンインスタンスを破棄
                foreach (var instance in _singletonInstances.Values)
                {
                    if (instance is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error disposing singleton service: {ex.Message}");
                        }
                    }
                }

                _singletonInstances.Clear();
                _services.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// サービススコープの実装
    /// </summary>
    internal class ServiceScope : IServiceScope
    {
        private readonly SimpleServiceContainer _container;
        private bool _disposed = false;

        public IServiceContainer ServiceProvider => _container;

        public ServiceScope(SimpleServiceContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _container.ClearScopedInstances();
            _disposed = true;
        }
    }
}