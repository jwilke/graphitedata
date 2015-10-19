using Microsoft.Framework.DependencyInjection;
using StructureMap;
using StructureMap.Configuration.DSL.Expressions;
using System;
using System.Collections.Generic;

namespace GraphiteData.StructureMap
{
    public static class StructureMapRegistration
    {
        public static void Populate(this IContainer container, IEnumerable<ServiceDescriptor> descriptors)
        {
            var populator = new StructureMapPopulator(container);
            populator.Populate(descriptors);
        }
    }

    internal class StructureMapPopulator
    {
        private IContainer _container;

        public StructureMapPopulator(IContainer container)
        {
            _container = container;
        }

        public void Populate(IEnumerable<ServiceDescriptor> descriptors)
        {
            _container.Configure(c =>
            {
                c.For<IServiceProvider>().Use(new StructureMapServiceProvider(_container));
                c.For<IServiceScopeFactory>().Use<StructureMapServiceScopeFactory>();

                foreach (var descriptor in descriptors)
                {
                    switch (descriptor.Lifetime)
                    {
                        case ServiceLifetime.Singleton:
                            Use(c.For(descriptor.ServiceType).Singleton(), descriptor);
                            break;
                        case ServiceLifetime.Transient:
                            Use(c.For(descriptor.ServiceType), descriptor);
                            break;
                        case ServiceLifetime.Scoped:
                            Use(c.For(descriptor.ServiceType), descriptor);
                            break;
                    }
                }
            });
        }

        private static void Use(GenericFamilyExpression expression, ServiceDescriptor descriptor)
        {
            if (descriptor.ImplementationFactory != null)
            {
                expression.Use(Guid.NewGuid().ToString(), context => { return descriptor.ImplementationFactory(context.GetInstance<IServiceProvider>()); });
            }
            else if (descriptor.ImplementationInstance != null)
            {
                expression.Use(descriptor.ImplementationInstance);
            }
            else if (descriptor.ImplementationType != null)
            {
                expression.Use(descriptor.ImplementationType);
            }
            else
            {
                throw new InvalidOperationException("IServiceDescriptor is invalid");
            }
        }
    }

    internal class StructureMapServiceProvider : IServiceProvider
    {
        private readonly IContainer _container;

        public StructureMapServiceProvider(IContainer container, bool scoped = false)
        {
            _container = container;
        }

        public object GetService(Type type)
        {
            try
            {
                return _container.GetInstance(type);
            }
            catch
            {
                return null;
            }
        }
    }

    internal class StructureMapServiceScope : IServiceScope
    {
        private IContainer _container;
        private IContainer _childContainer;
        private IServiceProvider _provider;

        public StructureMapServiceScope(IContainer container)
        {
            _container = container;
            _childContainer = _container.GetNestedContainer();
            _provider = new StructureMapServiceProvider(_childContainer, true);
        }

        public IServiceProvider ServiceProvider
        {
            get { return _provider; }
        }

        public void Dispose()
        {
            _provider = null;
            if (_childContainer != null)
                _childContainer.Dispose();
        }
    }

    internal class StructureMapServiceScopeFactory : IServiceScopeFactory
    {
        private IContainer _container;

        public StructureMapServiceScopeFactory(IContainer container)
        {
            _container = container;
        }

        public IServiceScope CreateScope()
        {
            return new StructureMapServiceScope(_container);
        }
    }
}
