﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;

namespace VerifyGitHubVulnerabilities.Verify
{
    public class PackageVulnerabilitiesVerifier : IPackageVulnerabilitiesManagementService
    {
        private readonly IEntitiesContext _entitiesContext;

        public PackageVulnerabilitiesVerifier(
            IEntitiesContext entitiesContext)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public bool HasErrors { get; private set; }

        public void ApplyExistingVulnerabilitiesToPackage(Package package)
        {
            throw new NotImplementedException();
        }

        public Task UpdateVulnerabilityAsync(PackageVulnerability vulnerability, bool withdrawn)
        {
            if (vulnerability == null)
            {
                Console.Error.WriteLine("Null vulnerability passed to verifier! Continuing...");
                return Task.CompletedTask;
            }

            Console.WriteLine($"Verifying vulnerability {vulnerability.GitHubDatabaseKey}.");
            var existingVulnerability = _entitiesContext.Vulnerabilities
                .Include(v => v.AffectedRanges)
                .SingleOrDefault(v => v.GitHubDatabaseKey == vulnerability.GitHubDatabaseKey);

            if (withdrawn || !vulnerability.AffectedRanges.Any())
            {
                if (existingVulnerability != null)
                {
                    Console.Error.WriteLine(withdrawn ?
                        $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey} was withdrawn and should not be in DB!" :
                        $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey} affects no packages and should not be in DB!");
                    HasErrors = true;
                }

                return Task.CompletedTask;
            }

            if (existingVulnerability == null)
            {
                Console.Error.WriteLine($"Cannot find vulnerability {vulnerability.GitHubDatabaseKey} in DB!");
                HasErrors = true;
                return Task.CompletedTask;
            }

            if (existingVulnerability.Severity != vulnerability.Severity)
            {
                Console.Error.WriteLine(
                    $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey
                    }, severity does not match! GitHub: {vulnerability.Severity}, DB: {existingVulnerability.Severity}");
                HasErrors = true;
            }

            if (existingVulnerability.AdvisoryUrl != vulnerability.AdvisoryUrl)
            {
                Console.Error.WriteLine(
                    $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey
                    }, advisory URL does not match! GitHub: {vulnerability.AdvisoryUrl}, DB: { existingVulnerability.AdvisoryUrl}");
                HasErrors = true;
            }

            foreach (var range in vulnerability.AffectedRanges)
            {
                Console.WriteLine($"Verifying range affecting {range.PackageId} {range.PackageVersionRange}.");
                var existingRange = existingVulnerability.AffectedRanges
                    .SingleOrDefault(r => r.PackageId == range.PackageId && r.PackageVersionRange == range.PackageVersionRange);

                if (existingRange == null)
                {
                    Console.Error.WriteLine(
                        $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey
                        }, cannot find range {range.PackageId} {range.PackageVersionRange} in DB!");
                    HasErrors = true;
                    continue;
                }

                if (existingRange.FirstPatchedPackageVersion != range.FirstPatchedPackageVersion)
                {
                    Console.Error.WriteLine(
                        $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey
                        }, range {range.PackageId} {range.PackageVersionRange}, first patched version does not match! GitHub: {
                        range.FirstPatchedPackageVersion}, DB: {range.FirstPatchedPackageVersion}");
                    HasErrors = true;
                }

                var packages = _entitiesContext.Packages
                    .Where(p => p.PackageRegistration.Id == range.PackageId)
                    .Include(p => p.Vulnerabilities)
                    .ToList();

                var versionRange = VersionRange.Parse(range.PackageVersionRange);
                foreach (var package in packages)
                {
                    var version = NuGetVersion.Parse(package.NormalizedVersion);
                    if (versionRange.Satisfies(version) != package.Vulnerabilities.Contains(existingRange))
                    {
                        Console.Error.WriteLine(
                            $@"Vulnerability advisory {vulnerability.GitHubDatabaseKey
                            }, range {range.PackageId} {range.PackageVersionRange}, package {package.NormalizedVersion
                            } is not properly marked vulnerable to vulnerability!");
                        HasErrors = true;
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}