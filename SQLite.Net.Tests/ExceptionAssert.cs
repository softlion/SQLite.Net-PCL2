﻿using System;
using NUnit.Framework;

namespace SQLite.Net2.Tests
{
    public class ExceptionAssert : BaseTest
    {
        public static T Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T ex)
            {
                return ex;
            }

            Assert.Fail($"Expected exception of type {typeof (T)}.");

            return null;
        }
    }
}