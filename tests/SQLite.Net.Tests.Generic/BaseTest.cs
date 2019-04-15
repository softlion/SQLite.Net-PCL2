using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLite.Net.Tests
{
    public class BaseTest
    {
        [OneTimeSetUp]
        public void OneTimeInit()
        {
            SQLitePCL.Batteries_V2.Init();
        }
    }
}
