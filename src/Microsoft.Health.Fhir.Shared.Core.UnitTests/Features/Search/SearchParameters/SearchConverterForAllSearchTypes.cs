// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Health.Fhir.Core.UnitTests.Features.Search
{
    public class SearchConverterForAllSearchTypes : IClassFixture<SearchParameterFixtureData>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly SearchParameterFixtureData _fixtureData;

        public SearchConverterForAllSearchTypes(ITestOutputHelper outputHelper, SearchParameterFixtureData fixtureData)
        {
            _outputHelper = outputHelper;
            _fixtureData = fixtureData;
        }

        [Theory]
        [MemberData(nameof(GetAllSearchParameters))]
        public void CheckSearchParameter(
            string resourceType,
            string parameterName,
            Microsoft.Health.Fhir.ValueSets.SearchParamType searchParamType,
            string fhirPath,
            SearchParameterInfo parameterInfo)
        {
            SearchParameterToTypeResolver.Log = s => _outputHelper.WriteLine(s);

            _outputHelper.WriteLine("** Evaluating: " + fhirPath);

            var parsed = _fixtureData.Compiler.Parse(fhirPath);

            var componentExpressions = parameterInfo.Component
                .Select(x => (_fixtureData.SearchDefinitionManager.UrlLookup[x.DefinitionUrl].Type, _fixtureData.Compiler.Parse(x.Expression)))
                .ToArray();

            var results = SearchParameterToTypeResolver.Resolve(
                resourceType,
                (searchParamType, parsed),
                componentExpressions).ToArray();

            Assert.True(results.Any(), $"{parameterName} ({resourceType}) was not able to be mapped.");

            string listedTypes = string.Join(",", results.Select(x => x.ClassMapping.NativeType.Name));
            _outputHelper.WriteLine($"Info: {parameterName} ({searchParamType}) found {results.Length} types ({listedTypes}).");

            foreach (var result in results)
            {
                var found = _fixtureData.Manager.TryGetConverter(result.ClassMapping.NativeType, SearchIndexer.GetSearchValueTypeForSearchParamType(result.SearchParamType), out var converter);

                var converterText = found ? converter.GetType().Name : "None";
                string searchTermMapping = $"Search term '{parameterName}' ({result.SearchParamType}) mapped to '{result.ClassMapping.NativeType.Name}', converter: {converterText}";
                _outputHelper.WriteLine(searchTermMapping);

                Assert.True(
                    found,
                    searchTermMapping);
            }
        }

        public static IEnumerable<object[]> GetAllSearchParameters()
        {
            var manager = SearchParameterFixtureData.CreateSearchParameterDefinitionManager();

            var values = ModelInfoProvider.Instance
                .GetResourceTypeNames()
                .Select(resourceType => (resourceType, manager.GetSearchParameters(resourceType)));

            foreach (var row in values)
            {
                foreach (var p in row.Item2)
                {
                    if (p.Name != "_type")
                    {
                        yield return new object[] { row.resourceType, p.Name, p.Type, p.Expression, p };
                    }
                }
            }
        }
    }
}
