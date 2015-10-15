#region CPL License
/*
Nuclex Framework
Copyright (C) 2002-2012 Nuclex Development Labs

This library is free software; you can redistribute it and/or
modify it under the terms of the IBM Common Public License as
published by the IBM Corporation; either version 1.0 of the
License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
IBM Common Public License for more details.

You should have received a copy of the IBM Common Public
License along with this library
*/
#endregion

#if UNITTEST

using System;
using System.Reflection;

using NUnit.Framework;

namespace Nuclex.Support.Cloning {

  /// <summary>Unit Test for the cloner helpers</summary>
  [TestFixture]
  internal class ClonerHelpersTest {

    #region class Base

    /// <summary>Base class used to test the helper methods</summary>
    private class Base {
      /// <summary>A simple public field</summary>
      public int PublicBaseField;
      /// <summary>An automatic property with a hidden backing field</summary>
      public int PublicBaseProperty { get; set; }
    }

    #endregion // class Base

    #region class Derived

    /// <summary>Derived class used to test the helper methods</summary>
    private class Derived : Base {
      /// <summary>A simple public field</summary>
      public int PublicDerivedField;
      /// <summary>An automatic property with a hidden backing field</summary>
      public int PublicDerivedProperty { get; set; }
    }

    #endregion // class Derived

    /// <summary>
    ///   Verifies that the GetFieldInfosIncludingBaseClasses() will include the backing
    ///   fields of automatically implemented properties in base classes
    /// </summary>
    [Test]
    public void CanGetBackingFieldsForPropertiesInBaseClasses() {
      FieldInfo[] fieldInfos = ClonerHelpers.GetFieldInfosIncludingBaseClasses(
        typeof(Derived), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
      );
      Assert.AreEqual(4, fieldInfos.Length);
    }

  }

} // namespace Nuclex.Support.Cloning

#endif // UNITTEST
