﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hl7.Fhir.Serialization;

namespace Hl7.Fhir.Test.Serialization
{
   
    [TestClass]
    public class ClassFactoryListTest
    {
        [TestMethod]
        public void FindModelClassFactory()
        {
            ModelFactoryList facs = new ModelFactoryList();

            var specificFactory = new SpecificModelClassFactory();
            facs.Add(specificFactory);
            var defaultFactory = new DefaultModelFactory();
            facs.Add(defaultFactory);

            var selectedFactory = facs.FindFactory(typeof(SpecificModelClass));
            Assert.AreEqual(specificFactory, selectedFactory);

            selectedFactory = facs.FindFactory(typeof(GenericModelClass));
            Assert.AreEqual(defaultFactory, selectedFactory);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void FailWhenNoFactoryFound()
        {
            ModelFactoryList facs = new ModelFactoryList();

            facs.Add(new SpecificModelClassFactory());

            var result = facs.FindFactory(typeof(GenericModelClass));
        }
    }


    public class SpecificModelClass
    {
    }

    public class GenericModelClass
    {
    }

    public class SpecificModelClassFactory : IModelClassFactory
    {
        public bool CanCreate(Type type)
        {
            return type == typeof(SpecificModelClass);
        }

        public object Create(Type type)
        {
            throw new NotImplementedException();
        }
    }
}
