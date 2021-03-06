﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;

namespace NuGetGallery
{
    public class DisplayLicenseViewModelFactory
    {
        private readonly IReadMeService _readMeService;
        private PackageViewModelFactory _packageViewModelFactory;

        public DisplayLicenseViewModelFactory(IIconUrlProvider iconUrlProvider, IReadMeService readMeService)
        {
            _packageViewModelFactory = new PackageViewModelFactory(iconUrlProvider);
            _readMeService = readMeService;
        }

        public DisplayLicenseViewModel Create(
            Package package,
            IReadOnlyCollection<CompositeLicenseExpressionSegment> licenseExpressionSegments,
            string licenseFileContents)
        {
            var viewModel = new DisplayLicenseViewModel();
            return Setup(viewModel, package, licenseExpressionSegments, licenseFileContents);
        }

        private DisplayLicenseViewModel Setup(
            DisplayLicenseViewModel viewModel,
            Package package,
            IReadOnlyCollection<CompositeLicenseExpressionSegment> licenseExpressionSegments,
            string licenseFileContents)
        {
            _packageViewModelFactory.Setup(viewModel, package);
            return SetupInternal(viewModel, package, licenseExpressionSegments, licenseFileContents);
        }

        private DisplayLicenseViewModel SetupInternal(
            DisplayLicenseViewModel viewModel,
            Package package,
            IReadOnlyCollection<CompositeLicenseExpressionSegment> licenseExpressionSegments,
            string licenseFileContents)
        {
            viewModel.EmbeddedLicenseType = package.EmbeddedLicenseType;
            viewModel.LicenseExpression = package.LicenseExpression;
            if (PackageHelper.TryPrepareUrlForRendering(package.LicenseUrl, out string licenseUrl))
            {
                viewModel.LicenseUrl = licenseUrl;

                var licenseNames = package.LicenseNames;
                if (!string.IsNullOrEmpty(licenseNames))
                {
                    viewModel.LicenseNames = licenseNames.Split(',').Select(l => l.Trim()).ToList();
                }
            }
            viewModel.LicenseExpressionSegments = licenseExpressionSegments;
            viewModel.LicenseFileContents = licenseFileContents;

            if (package.EmbeddedLicenseType == EmbeddedLicenseFileType.Markdown && licenseFileContents != null)
            {
                // For some reason, the BOM character causes CommonMarkSettings to not work properly

                var newlinesFixed = licenseFileContents.Replace("\ufeff", "");
                viewModel.LicenseFileContentsMd = _readMeService.GetReadMeHtml(newlinesFixed).Content;
            }


            return viewModel;
        }
    }
}