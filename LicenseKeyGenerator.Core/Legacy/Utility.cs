using System;
using System.IO;
using System.Xml;
namespace Quickhand.Licensing.Legacy
{
    public static class Utility
    {
        public static bool GetXMLFileContent(string productCode, int trialTimes, long appExpriyDate, bool appFullAccess, 
            string deviceId, out string xmlContent, bool webAccess = false, bool 
            paymentActivated = false, long paymentExpiryDate = 0, int paymentTerminate = 1)
        {
            xmlContent = string.Empty;
            try
            {
                using (StringWriter sw = new StringWriter())
                {
                    XmlTextWriter writer = new XmlTextWriter(sw);
                    writer.WriteStartDocument(); // <?xml version="1.0" encoding="utf-16"?>
                    writer.WriteStartElement("license"); // license
                    writer.WriteStartElement("productcode");
                    writer.WriteString(productCode);
                    writer.WriteEndElement();

                    writer.WriteStartElement("trialtimes");
                    writer.WriteString(trialTimes.ToString());
                    writer.WriteEndElement();

                    writer.WriteStartElement("app");

                    writer.WriteStartAttribute("expirydate");
                    writer.WriteString(appExpriyDate.ToString());
                    writer.WriteEndAttribute();

                    writer.WriteStartAttribute("fullaccess");
                    writer.WriteString(appFullAccess ? "true" : "false");
                    writer.WriteEndAttribute();

                    writer.WriteStartElement("deviceid");
                    writer.WriteString(deviceId);
                    writer.WriteEndElement();

                    writer.WriteEndElement(); //end of app

                    writer.WriteStartElement("web");
                    writer.WriteString(webAccess ? "true" : "false");
                    writer.WriteEndElement(); // end of web

                    writer.WriteStartElement("payment");

                    writer.WriteStartAttribute("expirydate");
                    writer.WriteString(paymentExpiryDate.ToString());
                    writer.WriteEndAttribute();

                    writer.WriteStartAttribute("terminalamount");
                    writer.WriteString(paymentTerminate.ToString());
                    writer.WriteEndAttribute();

                    writer.WriteString(paymentActivated ? "true" : "false");
                    writer.WriteEndElement();  // end of payment

                    writer.WriteEndElement();//end of license
                    writer.WriteEndDocument(); // end of document
                    writer.Flush();
                    xmlContent = sw.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                xmlContent = string.Empty;
                return false;
            }

            return true;
        }

        public static long GetUnixTimeStamp(DateTime dt)
        {
            var timeSpan = (dt - new DateTime(1970, 1, 1, 0, 0, 0));
            return (long)timeSpan.TotalSeconds;
        }

        public static System.DateTime GetDateFromUnixTimeStamp(long time)
        {
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Local);
            return dtDateTime.AddSeconds(time).ToLocalTime();
        }
    }
}
