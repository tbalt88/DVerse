using System;
using DVerse.Plugins;
using Microsoft.Xrm.Sdk;
using Moq;
using Xunit;

namespace DVerse.Plugins.Tests
{
    public class MatterNumberValidatorTests
    {
        private static Mock<IServiceProvider> BuildServiceProvider(
            string entityName = "dv_matter",
            Entity target = null)
        {
            var mockContext = new Mock<IPluginExecutionContext>();
            var mockTracingService = new Mock<ITracingService>();
            var mockServiceProvider = new Mock<IServiceProvider>();

            mockContext.Setup(c => c.PrimaryEntityName).Returns(entityName);

            var inputParams = new ParameterCollection();
            if (target != null)
                inputParams["Target"] = target;
            mockContext.Setup(c => c.InputParameters).Returns(inputParams);

            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(IPluginExecutionContext)))
                .Returns(mockContext.Object);
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(ITracingService)))
                .Returns(mockTracingService.Object);

            return mockServiceProvider;
        }

        private static Entity BuildMatter(string matterNumber = null, bool includeAttribute = true)
        {
            var matter = new Entity("dv_matter") { Id = Guid.NewGuid() };
            if (includeAttribute)
                matter["dv_matternumber"] = matterNumber;
            return matter;
        }

        // Happy path.

        [Theory]
        [InlineData("M-1234")]
        [InlineData("M-99999")]
        [InlineData("M-0001")]
        public void Execute_ValidMatterNumber_DoesNotThrow(string value)
        {
            var provider = BuildServiceProvider(target: BuildMatter(value));
            var ex = Record.Exception(() => new MatterNumberValidator().Execute(provider.Object));
            Assert.Null(ex);
        }

        // Violation.

        [Theory]
        [InlineData("M-123")]      // too few digits
        [InlineData("1234")]       // missing M- prefix
        [InlineData("m-1234")]     // wrong case prefix
        [InlineData("M-12A4")]     // non-digit in the number
        [InlineData("M1234")]      // missing hyphen
        [InlineData("")]           // empty
        public void Execute_InvalidMatterNumber_ThrowsInvalidPluginExecutionException(string value)
        {
            var provider = BuildServiceProvider(target: BuildMatter(value));
            Assert.Throws<InvalidPluginExecutionException>(
                () => new MatterNumberValidator().Execute(provider.Object));
        }

        [Fact]
        public void Execute_InvalidMatterNumber_ExceptionNamesValueAndExpectedFormat()
        {
            const string badValue = "M-12";
            var provider = BuildServiceProvider(target: BuildMatter(badValue));
            var ex = Assert.Throws<InvalidPluginExecutionException>(
                () => new MatterNumberValidator().Execute(provider.Object));
            Assert.Contains(badValue, ex.Message);
            Assert.Contains("M-", ex.Message);
            Assert.Contains("four or more digits", ex.Message);
        }

        // Absent attribute.

        [Fact]
        public void Execute_MatterNumberAttributeAbsent_DoesNotThrow()
        {
            var provider = BuildServiceProvider(target: BuildMatter(includeAttribute: false));
            var ex = Record.Exception(() => new MatterNumberValidator().Execute(provider.Object));
            Assert.Null(ex);
        }

        // Non-target entity, no-op.

        [Fact]
        public void Execute_NonTargetEntity_NoOp()
        {
            var otherEntity = new Entity("account") { Id = Guid.NewGuid() };
            otherEntity["dv_matternumber"] = "definitely-not-a-valid-matter-number";
            var provider = BuildServiceProvider(entityName: "account", target: otherEntity);
            var ex = Record.Exception(() => new MatterNumberValidator().Execute(provider.Object));
            Assert.Null(ex);
        }

        [Fact]
        public void Execute_NonEntityTarget_NoOp()
        {
            // Delete's InputParameters carries an EntityReference under
            // "Target", not an Entity. The type check must skip cleanly
            // rather than throw an InvalidCastException.
            var mockContext = new Mock<IPluginExecutionContext>();
            var mockTracingService = new Mock<ITracingService>();
            var mockServiceProvider = new Mock<IServiceProvider>();

            mockContext.Setup(c => c.PrimaryEntityName).Returns("dv_matter");
            var inputParams = new ParameterCollection
            {
                ["Target"] = new EntityReference("dv_matter", Guid.NewGuid())
            };
            mockContext.Setup(c => c.InputParameters).Returns(inputParams);
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(IPluginExecutionContext)))
                .Returns(mockContext.Object);
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(ITracingService)))
                .Returns(mockTracingService.Object);

            var ex = Record.Exception(
                () => new MatterNumberValidator().Execute(mockServiceProvider.Object));
            Assert.Null(ex);
        }
    }
}
