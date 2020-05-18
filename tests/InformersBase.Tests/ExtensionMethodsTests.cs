using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Steeltoe.Informers.InformersBase.Tests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Steeltoe.Informers.InformersBase.Tests
{
    public class ExtensionMethodsTests
    {
        private readonly ILogger _log;

        public ExtensionMethodsTests(ITestOutputHelper output)
        {
            _log = new XunitLogger<SharedInformerTests>(output);
        }

        [Fact]
        public void ToReset_WhenEmitEmpty_ProduceSingleItem()
        {

           var sut = new Dictionary<string,string>().ToReset(true).ToList();

           sut.Should().BeEquivalentTo(new ResourceEvent<string, string>(EventTypeFlags.ResetEmpty));

        }
        [Fact]
        public void ToReset_TwoItems_CorrectReset()
        {
            var sut = new Dictionary<int,int>() {{1,1}, {2,2}}.ToReset(true).ToList();
            sut.Should().BeEquivalentTo(new ResourceEvent<int, int>(EventTypeFlags.ResetStart, 1, 1), new ResourceEvent<int, int>(EventTypeFlags.ResetEnd, 2, 2));
        }
    }
}