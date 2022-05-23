﻿using PnP.PowerShell.Commands.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Security;
using System.Security.Cryptography.X509Certificates;

namespace PnP.PowerShell.Commands.Base
{
    [Cmdlet(VerbsCommon.New, "PnPAzureCertificate")]
    [OutputType(typeof(Model.AzureCertificate))]
    public class NewPnPAdalCertificate : PSCmdlet
    {
        [Parameter(Mandatory = false, Position = 0)]
        public string CommonName = "pnp.contoso.com";

        [Parameter(Mandatory = false, Position = 1)]
        public string Country = String.Empty;

        [Parameter(Mandatory = false, Position = 2)]
        public string State = string.Empty;

        [Parameter(Mandatory = false, Position = 3)]
        public string Locality = string.Empty;

        [Parameter(Mandatory = false, Position = 4)]
        public string Organization = string.Empty;

        [Parameter(Mandatory = false, Position = 5)]
        public string OrganizationUnit = string.Empty;

        [Parameter(Mandatory = false, Position = 6)]
        public string OutPfx;

        [Parameter(Mandatory = false, Position = 6)]
        public string OutCert;

        [Parameter(Mandatory = false, Position = 7)]
        public int ValidYears = 10;

        [Parameter(Mandatory = false, Position = 8)]
        public SecureString CertificatePassword;

        [Parameter(Mandatory = false)]
        public StoreLocation Store;

        protected override void ProcessRecord()
        {
            if (MyInvocation.BoundParameters.ContainsKey(nameof(Store)) && !Utilities.OperatingSystem.IsWindows())
            {
                throw new PSArgumentException("The Store parameter is only supported on Microsoft Windows");
            }

            if (ValidYears < 1 || ValidYears > 30)
            {
                ValidYears = 10;
            }
            DateTime validFrom = DateTime.Today;
            DateTime validTo = validFrom.AddYears(ValidYears);


#if NETFRAMEWORK
            var x500Values = new List<string>();
            if (!string.IsNullOrWhiteSpace(CommonName)) x500Values.Add($"CN={CommonName}");
            if (!string.IsNullOrWhiteSpace(Country)) x500Values.Add($"C={Country}");
            if (!string.IsNullOrWhiteSpace(State)) x500Values.Add($"S={State}");
            if (!string.IsNullOrWhiteSpace(Locality)) x500Values.Add($"L={Locality}");
            if (!string.IsNullOrWhiteSpace(Organization)) x500Values.Add($"O={Organization}");
            if (!string.IsNullOrWhiteSpace(OrganizationUnit)) x500Values.Add($"OU={OrganizationUnit}");

            string x500 = string.Join("; ", x500Values);

            byte[] certificateBytes = CertificateHelper.CreateSelfSignCertificatePfx(x500, validFrom, validTo, CertificatePassword);
            X509Certificate2 certificate = new X509Certificate2(certificateBytes, CertificatePassword, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
#else
            X509Certificate2 certificate = CertificateHelper.CreateSelfSignedCertificate(CommonName, Country, State, Locality, Organization, OrganizationUnit, CertificatePassword, CommonName, validFrom, validTo);
#endif

            if (!string.IsNullOrWhiteSpace(OutPfx))
            {
                if (!Path.IsPathRooted(OutPfx))
                {
                    OutPfx = Path.Combine(SessionState.Path.CurrentFileSystemLocation.Path, OutPfx);
                }
                byte[] certData = certificate.Export(X509ContentType.Pfx, CertificatePassword);
                File.WriteAllBytes(OutPfx, certData);
            }

            if (!string.IsNullOrWhiteSpace(OutCert))
            {
                if (!Path.IsPathRooted(OutCert))
                {
                    OutCert = Path.Combine(SessionState.Path.CurrentFileSystemLocation.Path, OutCert);
                }
                byte[] certData = certificate.Export(X509ContentType.Cert);
                File.WriteAllBytes(OutCert, certData);
            }

            if (Utilities.OperatingSystem.IsWindows() && MyInvocation.BoundParameters.ContainsKey(nameof(Store)))
            {
                using (var store = new X509Store("My", Store))
                {
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(certificate);
                    store.Close();
                }
                Host.UI.WriteLine(ConsoleColor.Yellow, Host.UI.RawUI.BackgroundColor, "Certificate added to store");
            }

            GetPnPAdalCertificate.WriteAzureCertificateOutput(this, certificate, CertificatePassword);
        }
    }
}
