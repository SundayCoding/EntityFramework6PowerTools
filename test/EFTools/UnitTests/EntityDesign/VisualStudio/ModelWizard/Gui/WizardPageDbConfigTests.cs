﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Data.Entity.Design.VisualStudio.ModelWizard.Gui
{
    using System.Xml;
    using EnvDTE;
    using Microsoft.Data.Entity.Design.VisualStudio.ModelWizard.Engine;
    using Microsoft.Data.Entity.Design.VisualStudio.Package;
    using Microsoft.VisualStudio.Data.Core;
    using Moq;
    using System;
    using System.Collections.Generic;
    using UnitTests.TestHelpers;
    using VSLangProj;
    using Xunit;

    public class WizardPageDbConfigTests
    {
        static WizardPageDbConfigTests()
        {
            // The code below is required to avoid test failures due to:
            // Due to limitations in CLR, DynamicProxy was unable to successfully replicate non-inheritable attribute 
            // System.Security.Permissions.UIPermissionAttribute on 
            // Microsoft.Data.Entity.Design.VisualStudio.ModelWizard.Gui.WizardPageStart.ProcessDialogChar. 
            // To avoid this error you can chose not to replicate this attribute type by calling
            // 'Castle.DynamicProxy.Generators.AttributesToAvoidReplicating.Add(typeof(System.Security.Permissions.UIPermissionAttribute))'.
            // 
            // Note that the same pattern need to be used when creating tests for other wizard pages to avoid 
            // issues related to the order the tests are run. Alternatively we could have code that is always being run
            // before any tests (e.g. a ctor of a class all test classes would derive from) where we would do that
            Castle.DynamicProxy.Generators
                .AttributesToAvoidReplicating
                .Add(typeof(System.Security.Permissions.UIPermissionAttribute));
        }

        [Fact]
        public void OnActivate_result_depends_on_FileAlreadyExistsError()
        {
            var wizard = ModelBuilderWizardFormHelper.CreateWizard();
            var wizardPageDbConfig = new WizardPageDbConfig(wizard, CreateConfigFileUtils());

            wizard.FileAlreadyExistsError = true;
            Assert.False(wizardPageDbConfig.OnActivate());

            wizard.FileAlreadyExistsError = false;
            Assert.True(wizardPageDbConfig.OnActivate());
        }

        [Fact]
        public void GetTextBoxConnectionStringValue_returns_entity_connection_string_for_EDMX_DatabaseFirst()
        {
            var guid = new Guid("42424242-4242-4242-4242-424242424242");

            var mockDte = new MockDTE(".NETFramework, Version=v4.5", references: new Reference[0]);
            mockDte.SetProjectProperties(new Dictionary<string, object> { { "FullPath", @"C:\Project" } });
            var mockParentProjectItem = new Mock<ProjectItem>();
            mockParentProjectItem.Setup(p => p.Collection).Returns(Mock.Of<ProjectItems>());
            mockParentProjectItem.Setup(p => p.Name).Returns("Folder");

            var mockModelProjectItem = new Mock<ProjectItem>();
            var mockCollection = new Mock<ProjectItems>();
            mockCollection.Setup(p => p.Parent).Returns(mockParentProjectItem.Object);
            mockModelProjectItem.Setup(p => p.Collection).Returns(mockCollection.Object);

            var wizardPageDbConfig =
                new WizardPageDbConfig(
                    ModelBuilderWizardFormHelper.CreateWizard(ModelGenerationOption.GenerateFromDatabase, mockDte.Project, @"C:\Project\myModel.edmx"),
                    CreateConfigFileUtils());

            Assert.Equal(
                "metadata=res://*/myModel.csdl|res://*/myModel.ssdl|res://*/myModel.msl;provider=System.Data.SqlClient;" +
                "provider connection string=\"integrated security=SSPI;MultipleActiveResultSets=True;App=EntityFramework\"",
                wizardPageDbConfig.GetTextBoxConnectionStringValue(
                    CreateDataProviderManager(guid),
                    guid,
                    "Integrated Security=SSPI"));
        }

        [Fact]
        public void GetTextBoxConnectionStringValue_returns_regular_connection_string_for_CodeFirst_from_Database()
        {
            var guid = new Guid("42424242-4242-4242-4242-424242424242");
            var wizardPageDbConfig = new WizardPageDbConfig(
                ModelBuilderWizardFormHelper.CreateWizard(ModelGenerationOption.CodeFirstFromDatabase),
                CreateConfigFileUtils());

            Assert.Equal(
                "integrated security=SSPI;MultipleActiveResultSets=True;App=EntityFramework",
                wizardPageDbConfig.GetTextBoxConnectionStringValue(
                    CreateDataProviderManager(guid),
                    guid, 
                    "Integrated Security=SSPI"));
        }

        private static IVsDataProviderManager CreateDataProviderManager(Guid vsDataProviderGuid)
        {
            var mockDataProvider = new Mock<IVsDataProvider>();
            mockDataProvider
                .Setup(p => p.GetProperty("InvariantName"))
                .Returns("System.Data.SqlClient");

            var mockProviderManager = new Mock<IVsDataProviderManager>();
            mockProviderManager
                .Setup(m => m.Providers)
                .Returns(new Dictionary<Guid, IVsDataProvider> { { vsDataProviderGuid, mockDataProvider.Object } });

            return mockProviderManager.Object;
        }

        [Fact]
        public void GetUniqueConnectionStringName_returns_candidate_connection_string_name_if_config_does_not_exist()
        {
            var wizard = ModelBuilderWizardFormHelper.CreateWizard(
                project: Mock.Of<Project>(), serviceProvider: Mock.Of<IServiceProvider>());

            var wizardPageDbConfig =
                new WizardPageDbConfig(wizard, CreateConfigFileUtils());

            Assert.Equal("myModel", wizardPageDbConfig.GetUniqueConnectionStringName("myModel"));
        }

        [Fact]
        public void GetUniqueConnectionStringName_uniquifies_proposed_connection_string_name()
        {
            var configXml = new XmlDocument();
            configXml.LoadXml(@"<configuration>
  <connectionStrings>    
    <add name=""myModel"" connectionString=""Data Source=(localdb)\v11.0;"" providerName=""System.Data.SqlClient"" />
    <add name=""myModel1"" connectionString=""metadata=res://*;"" providerName=""System.Data.EntityClient"" />
    <add name=""myModel2"" connectionString=""metadata=res://*;"" providerName=""System.Data.SqlCe"" />
  </connectionStrings>
</configuration>");

            var wizard = ModelBuilderWizardFormHelper.CreateWizard(
                project: Mock.Of<Project>(), serviceProvider: Mock.Of<IServiceProvider>());

            var mockConfig =
                new Mock<ConfigFileUtils>(Mock.Of<Project>(), Mock.Of<IServiceProvider>(), Mock.Of<IVsUtils>(), null);
            mockConfig
                .Setup(c => c.LoadConfig())
                .Returns(configXml);

            var wizardPageDbConfig = new WizardPageDbConfig(wizard, mockConfig.Object);

            Assert.Equal("myModel3", wizardPageDbConfig.GetUniqueConnectionStringName("myModel"));
        }

        private static ConfigFileUtils CreateConfigFileUtils()
        {
            return new Mock<ConfigFileUtils>(Mock.Of<Project>(), Mock.Of<IServiceProvider>(), Mock.Of<IVsUtils>(), null).Object;
        }

    }
}
