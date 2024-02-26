//https://docs.nunit.org/articles/nunit/release-notes/Nunit4.0-MigrationGuide.html
global using Assert = NUnit.Framework.Legacy.ClassicAssert;
global using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
global using StringAssert = NUnit.Framework.Legacy.StringAssert;
global using DirectoryAssert = NUnit.Framework.Legacy.DirectoryAssert;
global using FileAssert = NUnit.Framework.Legacy.FileAssert;


using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLite.Net2.Tests
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
