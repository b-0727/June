using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Pulsar.Server.Extensions
{
    public static class X509CertificateExtensions
    {
        private const string SubjectAlternativeNameOid = "2.5.29.17";

        public static IReadOnlyCollection<string> GetSubjectAlternativeNames(this X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                return Array.Empty<string>();
            }

            var extension = certificate.Extensions.Cast<X509Extension?>()
                .FirstOrDefault(ext => ext?.Oid?.Value == SubjectAlternativeNameOid);

            if (extension == null)
            {
                return Array.Empty<string>();
            }

            var names = new List<string>();

            try
            {
                var reader = new AsnReader(extension.RawData, AsnEncodingRules.DER);
                var sequence = reader.ReadSequence();

                while (sequence.HasData)
                {
                    var tag = sequence.PeekTag();
                    if (tag.TagClass != TagClass.ContextSpecific)
                    {
                        sequence.ReadEncodedValue();
                        continue;
                    }

                    switch (tag.TagValue)
                    {
                        case 2:
                            var dnsName = sequence.ReadCharacterString(UniversalTagNumber.IA5String, new Asn1Tag(TagClass.ContextSpecific, 2));
                            if (!string.IsNullOrWhiteSpace(dnsName))
                            {
                                names.Add(dnsName);
                            }
                            break;
                        case 7:
                            var ipBytes = sequence.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 7));
                            if (ipBytes?.Length > 0)
                            {
                                names.Add(new IPAddress(ipBytes).ToString());
                            }
                            break;
                        default:
                            sequence.ReadEncodedValue();
                            break;
                    }
                }
            }
            catch (AsnContentException)
            {
                return Array.Empty<string>();
            }

            return names;
        }
    }
}
