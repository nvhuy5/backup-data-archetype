using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleApp1.NavWS;

namespace ConsoleApp1
{
    static class ClientSynchro
    {



        public static string CheckCompanyCode(MQMessage message)
        {
            Client_Service WSclient = new NavWS.Client_Service();

            WSclient.Url = "http://" + message.nav_server + "/WS/" + message.act_legal.Replace(" ", "%20") + "/Page/Client";
            WSclient.Credentials = new System.Net.NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");
            //http://ACT-NAV2013.archetype-group.com:7057/Archetype/WS/Archetype%20Thailand%20-%20BKK/Page/Client
            Client customer;
            try
            {
                customer = WSclient.Read(message.company_no);

                if (customer.No != null)
                {
                    message.Navision_Code = customer.No;
                    message.company_no = customer.No;
                    return customer.No;
                }
            }
            catch (Exception e)
            {
                
                return "";
            }
            return "";
        }

            public static int ProcessCreation(MQMessage message)
        {

             

            Client_Service WSclient = new NavWS.Client_Service();

            WSclient.Url = "http://" + message.nav_server + "/WS/" + message.act_legal.Replace(" ", "%20") + "/Page/Client";
            WSclient.Credentials = new System.Net.NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");

  
            message.Navision_Code = message.company_name.ToUpper().Substring(0, Math.Min(message.company_name.Length, 3));
           
            message.Navision_Code += "-";
            message.Navision_Code += message.CountryCallingCode;
           
            message.Navision_Code += "-";
            message.Navision_Code += message.sector_id;

            int Increment_code;

             string found_navcode;
            found_navcode = IsClientExist(ref WSclient, message);
          
            if (found_navcode != "")
            {
                message.Navision_Code = found_navcode;
                message.company_no = found_navcode;
             
                AlreadyExistCustomer(message);
              
                return 1;
            }

            Client[] custList;
          
            List<Client_Filter> filterArray = new List<Client_Filter>();
            Client_Filter nameFilter = new Client_Filter();
            nameFilter.Field = Client_Fields.No;
            nameFilter.Criteria = message.Navision_Code + "*";
            filterArray.Add(nameFilter);
            custList = WSclient.ReadMultiple(filterArray.ToArray(), null, 100);

           

            Increment_code = custList.Length;
            Increment_code++;
           
            message.Navision_Code += "-";
            message.Navision_Code += Increment_code.ToString().PadLeft(4, '0');
           
           
            message.company_no = message.Navision_Code;
          
            NavWS.Client newcli = new NavWS.Client();
            newcli.No = message.company_no;
            newcli.Name = message.legal_name;
            newcli.Search_Name = message.company_name;
            newcli.Vietnamese_Name = message.legal_name;
            newcli.Name_2 = message.legal_name;
            newcli.Business_Registration = message.registration_no;
            newcli.VAT_Registration_No = message.tax_no;
            newcli.Address = message.address;
            newcli.Customer_Posting_Group = "TRADE";
            newcli.Gen_Bus_Posting_Group = "TRADE";
            newcli.VAT_Bus_Posting_Group = message.VAT_posting;
            newcli.Copy_Sell_to_Addr_to_Qte_From = Copy_Sell_to_Addr_to_Qte_From.Company;
            newcli.Country_Region_Code = message.country_code;
          
            try {
                WSclient.Create(ref newcli);
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Requested value"))
                {
                    newcli.Country_Region_Code = "";
                    WSclient.Create(ref newcli);
                }
                else throw;
                return 0;
            }
            return 1;

        }


        public static string IsClientExist(ref Client_Service WSclient, MQMessage ClientToTest)
        {
            int found = 0;
            Client[] custList;
            List<Client_Filter> filterArray = new List<Client_Filter>();
        
            filterArray = new List<Client_Filter>();
            Client_Filter CodeFilter = new Client_Filter();
            CodeFilter.Field = Client_Fields.No;
            CodeFilter.Criteria = ClientToTest.Navision_Code;
            filterArray.Add(CodeFilter);
            custList = WSclient.ReadMultiple(filterArray.ToArray(), null, 100);
            if (custList.Length >= 1) return custList.First().No;

         
            if (ClientToTest.tax_no.Length > 0)
            {
                filterArray = new List<Client_Filter>();
                Client_Filter TaxNumFilter = new Client_Filter();
                TaxNumFilter.Field = Client_Fields.VAT_Registration_No;
                TaxNumFilter.Criteria = ClientToTest.tax_no;
                filterArray.Add(TaxNumFilter);
                custList = WSclient.ReadMultiple(filterArray.ToArray(), null, 100);
                if (custList.Length >= 1) return custList.First().No;
            }
       
            if (ClientToTest.registration_no.Length > 0)
            {
                filterArray = new List<Client_Filter>();
                Client_Filter RegNumFilter = new Client_Filter();
                RegNumFilter.Field = Client_Fields.Business_Registration;
                RegNumFilter.Criteria = ClientToTest.registration_no;
                filterArray.Add(RegNumFilter);
                custList = WSclient.ReadMultiple(filterArray.ToArray(), null, 100);
                if (custList.Length >= 1) return custList.First().No;
            }
       
            
            Client_Filter nameFilter = new Client_Filter();
            nameFilter.Field = Client_Fields.Vietnamese_Name;
            nameFilter.Criteria = "*" + ClientToTest.legal_name + "*";
            filterArray.Add(nameFilter);
            try
            {
                custList = WSclient.ReadMultiple(filterArray.ToArray(), null, 100);
       
                if (custList.Length >= 1) return custList.First().No;
       
            }
            catch
            { }




            return "";
        }

        public static async Task<bool> GetNextStep(string connectionString, string query)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("SQL query cannot be null or empty.", nameof(query));
            try
            {
                var conn = new SqlConnection(connectionString);
                var cmd = new SqlCommand(query, conn);

                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static void AlreadyExistCustomer(MQMessage message)
        {
            string recipients = ConsoleApp1.Properties.Settings.Default.MailRecipients;
          
             string subject = "[Success] Client Creation for Nav : " + message.act_legal;
            string content = @"Dear Colleague,<br><br>
Attention : We detected a customer potentially already exist in the system, then we use his code. Please check twice.
<br>Herebelow details :<br><br>
<table>
<tr><td><b>Customer Legal Name</b></td><td>" + message.legal_name + @"</td></tr>

<tr><td><b>Customer Nav Code</b></td><td>" + message.company_no + @"</td></tr>
<tr><td><b>Customer VAT code</b></td><td>" + message.tax_no + @"</td></tr>
<tr><td><b>Customer Reg. Code</b></td><td>" + message.registration_no + @"</td></tr>
<tr><td><b>Contract </b></td><td>" + message.contract_title + @"</td></tr> 
<tr><td><b>Contract Code </b></td><td>" + message.contract_code + @"</td></tr> 
<tr><td><b>Access to the Client Validation Form</b></td><td><a href ='https://www.archetype-intranet.com/crm/validate_client_information_for_finance.php?ac=v&client_id=" + message.client_financial_id + @"'>Link</a></td></tr> 
</table>
<br><br>
For any question, please contact dev-support@archetype-group.com
<br><br>
Best regards,
<br>Information System Team

";
            IntranetMailing.SendMail(subject, content, recipients);
        }
            public static void SendSuccessMail(MQMessage message)
        {
            string recipients = ConsoleApp1.Properties.Settings.Default.MailRecipients;
            if (ConsoleApp1.Properties.Settings.Default.IsTest != "yes") recipients += "," + message.validator_mail + "," + message.bdd_mail +"," + message.fc_mail  +"," + message.fm_mail;
            string subject = "[Success] Client Creation for Nav : " + message.act_legal;
            string content = @"Dear Colleague,<br><br>
This mail follow a successfull customer creation in our Financial System (Navision). 
<br>Herebelow details :<br><br>
<table>
<tr><td><b>Customer Legal Name</b></td><td>" + message.legal_name + @"</td></tr>

<tr><td><b>Customer Nav Code</b></td><td>" + message.company_no + @"</td></tr>
<tr><td><b>Customer VAT code</b></td><td>" + message.tax_no + @"</td></tr>
<tr><td><b>Customer Reg. Code</b></td><td>" + message.registration_no + @"</td></tr>
<tr><td><b>Contract </b></td><td>" + message.contract_title + @"</td></tr> 
<tr><td><b>Contract Code </b></td><td>" + message.contract_code + @"</td></tr> 
<tr><td><b>Access to the Client Validation Form</b></td><td><a href ='https://www.archetype-intranet.com/crm/validate_client_information_for_finance.php?ac=v&client_id=" + message.client_financial_id + @"'>Link</a></td></tr> 
</table>
<br><br>
For any question, please contact dev-support@archetype-group.com
<br><br>
Best regards,
<br>Information System Team
";
            IntranetMailing.SendMail(subject, content, recipients);
        }

    }
}
