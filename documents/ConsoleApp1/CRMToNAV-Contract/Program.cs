using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;
using System.Data.SqlClient;
using System.Text.Json;

namespace ConsoleApp1
{
    using EasyNetQ;
    using NavWS;
   
    using EasyNetQ.NonGeneric;
    using System.Net;
    using MySql.Data.MySqlClient;
    using System.Data;
    using System.Configuration;
    using ConsoleApp1.ContractsWS;
    using System.Data.SqlClient;
    using ConsoleApp1.DevDimWS;
    using ConsoleApp1.IntranetCodes;
    using System.IO;
    using System.Security.Cryptography;
    using RabbitMQ.Client.Framing.Impl;
    using System.Threading.Tasks;
    using System.Net.Http;
    using Newtonsoft.Json;

    class Program
    {



        static public DataTable GetCurrencyCode()
        {
            bool fireAgain = true;

           // string connectionString = ConsoleApp1.Properties.Settings.Default.NavSQL;
            string connectionString =
    "Data Source=act-nav2013.archetype-group.com;" +
    "Initial Catalog=Datawarehouse_ACT;" +
    "User ID=usr_etl;" +
    "Password=123qwe!@#$%;";
            SqlCommand comm;
            var forexconn = new SqlConnection(connectionString);
             forexconn.Open();
            string query = " select distinct gl.ACTEntityId, case when gl.LCYCode = 'TMY' then 'MMK' else gl.LCYCode end as LCYCode, " +
               "case when exr.[Currency Code] = 'SGP' then 'SGD' " +
               " when exr.[Currency Code] = 'TMY' then 'MMK' " +
               " when  exr.[Currency Code] = 'EURO' then 'EUR' else exr.[Currency Code] end  as ForeignCurrCode " +
               "   from Dim_GLSetup gl join Fact_CurrencyExRate exr on gl.ACTEntityId = exr.ACTEntityId ";
            var cmd = new SqlCommand(query, forexconn);

            comm = new SqlCommand(query, forexconn);
            comm.CommandType = CommandType.Text;
            SqlDataReader reader = comm.ExecuteReader();

            DataTable tb = new DataTable();
            tb.Load(reader);
            return tb;
        }

       

        public static decimal GetConversionRateAsync(string foreigncurr, string localcurr, HttpClient http)
        {
            string url = $"https://v6.exchangerate-api.com/v6/abfad78313310a81d82036aa/pair/{foreigncurr}/{localcurr}";
           
            try
            {
                using (var client = new HttpClient())
                {
                    // Synchronous call
                    HttpResponseMessage response = client.GetAsync(url).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();

                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    dynamic obj = JsonConvert.DeserializeObject(json);

                    return (decimal)obj.conversion_rate;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("API error: " + ex.Message);
                return 0m; // or throw; depending on your logic
            }
        }

        static public decimal GetExchangeRate(string localcurr, string foreigncurr)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;
            string url = "https://v6.exchangerate-api.com/v6/abfad78313310a81d82036aa/pair/" + foreigncurr + "/" + localcurr;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            

            // Common browser headers
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
         
            request.ProtocolVersion = HttpVersion.Version11;

            request.KeepAlive = true;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            string data = string.Empty;
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream receiveStream = response.GetResponseStream();
                StreamReader readStream = null;

                if (response.CharacterSet == null)
                {
                    readStream = new StreamReader(receiveStream);
                }
                else
                {
                    readStream = new StreamReader(receiveStream, System.Text.Encoding.GetEncoding(response.CharacterSet));
                }

                data = readStream.ReadToEnd();
                response.Close();
                readStream.Close();

            }
            data = data.Replace("{", "").Replace("}", "");

            decimal ret = 0;
            Decimal.TryParse(data.Substring(data.LastIndexOf(":") + 1), out ret);
            return ret;
        }

        static int Upsert(int ACTEntityId, string CurrencyCode, decimal ExchangeAmt)
        {
            string connectionString = ConsoleApp1.Properties.Settings.Default.NavSQL;

       
            var forexupconn = new SqlConnection(connectionString);
            forexupconn.Open();
            SqlCommand cmd = new SqlCommand("[dbo].[ForeignExchange_Upsert]", forexupconn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@ACTEntityId", ACTEntityId));
            cmd.Parameters.Add(new SqlParameter("@CurrencyCode", CurrencyCode));
            cmd.Parameters.Add(new SqlParameter("@ExchageVal", ExchangeAmt));
            int ret = cmd.ExecuteNonQuery();
            return ret;
        }


        static void Main(string[] args)
        {
         
            int contractmode = 0;
            int communicationmod = 1; 
            string ContractCode = "";

            DataTable dt = Program.GetCurrencyCode();
            foreach (DataRow row in dt.Rows)
            {
                string LocalCurrCode = row["LCYCode"].ToString();
                int ACTEntityId = (int)row["ACTEntityId"];
                string ForeignCurrCode = row["ForeignCurrCode"].ToString();
                //decimal ExRate = GetExchangeRate(LocalCurrCode, ForeignCurrCode);
                HttpClient http = new HttpClient();
                decimal ExRate = GetConversionRateAsync(LocalCurrCode, ForeignCurrCode, http);
                
                Upsert(ACTEntityId, ForeignCurrCode, ExRate);
                Console.WriteLine("Updating Forex : \n");
                Console.WriteLine("Entity :" + ACTEntityId.ToString() + "; Foreign Cur: "+ ForeignCurrCode.ToString() + " : OK\n");

            }



            Program.ProcessContracts(contractmode, ContractCode, communicationmod);


        }

        static public void ProcessPaymentsScheduleSynchro()
        {

        }



        static public void ProcessContracts(int SyncMode, string ContractCode, int CommunicationMode)
        {


            string connectionString = ConsoleApp1.Properties.Settings.Default.IntranetSQL;
            MySqlConnection connection = null;
            MySqlConnection connectionExec = null;
            MySqlDataReader reader = null;
            MySqlDataReader readerExec = null;

            try
            {
                connection = new MySqlConnection(connectionString);
                connection.Open();
                connectionExec = new MySqlConnection(connectionString);
                connectionExec.Open();
              
                string CheckContractToSync = @"SELECT  c.proj_id as project_id, c.refer_contract_id as main_contract_id, mc.code as crm_main_contract_code, c.id as contract_id, CASE WHEN c.contract_type = 'A' THEN 'Addendum' WHEN c.contract_type = 'C' THEN 'Contract' END as Contract_type , IFNULL(c.biz_revision,'3') as next_status, c.title as contract_name,c.manager_in_charge as PM_email, c.seller,pm.code as PM_code, IFNULL(IFNULL(IFNULL(md.email,md_2.email),salesoffice.managing_director) ,'hien.ntt@archetype-group.com') as MD_email, c.id as contract_id, proj.pname, salesoffice.idf as office_id, c.code  as contract_code, count(compo.crm_component_id) as CompoNb, sum(compo.component_value) as TCV,
IFNULL(IFNULL(fm.email,fm_2.email), 'hien.ntt@archetype-group.com') as FM_email,
IFNULL(IFNULL(fc.email,fc_2.email) ,'hien.ntt@archetype-group.com') as FC_email
                FROM crm_table_contracts c
                inner join hr_table_main pm on pm.arche_email = c.manager_in_charge
                left join crm_table_contracts mc on mc.id = c.refer_contract_id
                
                inner join crm_table_projects proj on proj.idproj = c.proj_id
                left join crm_finance_client_information cfi on c.crm_finance_client_information_id = cfi.crm_finance_client_information_id  
                left join crm_list_proposal_contract_data actlegal on actlegal.code = c.country_initial and actlegal.type = 'legal_entity'
                left join eai_navisionws_config nav on actlegal.eai_navision_company = nav.id and nav.isValid = 1
                inner join hr_menu_offices salesoffice on c.office_id = salesoffice.idf
                left join prm_purchase_order_position md on md.office_code = salesoffice.office_code and md.position= 'Managing director'
                left join prm_purchase_order_position md_2 on md_2.country_code = salesoffice.office_code and md_2.position= 'Managing director'
                left join prm_purchase_order_position fc on fc.office_code = salesoffice.office_code and fc.position= 'Finance controller'
                left join prm_purchase_order_position fc_2 on fc_2.country_code = salesoffice.office_code and fc_2.position= 'Finance controller'
                left join prm_purchase_order_position fm on fm.office_code = salesoffice.office_code and fm.position= 'Finance manager'
                left join prm_purchase_order_position fm_2 on fm_2.country_code = salesoffice.office_code and fm_2.position= 'Finance manager' 
                   
                inner join hr_menu_offices categoffice on c.category_office = categoffice.idf
                left join hr_menu_country cficountry on cfi.country = cficountry.idp
                inner join crm_component compo on compo.contract_id = c.id
                    ";
       
                if (ContractCode != "")
                {
                    CheckContractToSync += " where c.code In ("+ContractCode+");";
                }
                else {
                    CheckContractToSync += " where c.validate_status = 2 group by c.id, c.code, proj.pname;";
                 }
                


               
                MySqlDataAdapter dataAdapter = new MySqlDataAdapter();
                dataAdapter.SelectCommand = new MySqlCommand(CheckContractToSync, connection);
                DataTable ContractToCreate = new DataTable();
                dataAdapter.Fill(ContractToCreate);
                Console.WriteLine(ContractToCreate.Rows.Count.ToString() + "Contract found");

                foreach (DataRow Contract in ContractToCreate.Rows)
                {
                    int CRMContractId = Contract.Field<int>("contract_id");
                    int NbOfCompo = (int)Contract.Field<Int64>("CompoNb");
                    double TCV = Contract.Field<double>("TCV");
                
                    MQMessage message = new MQMessage();
                  
                    message.contract_id = Contract.Field<int>("contract_id").ToString();
                    message.project_id = Contract.Field<int>("project_id");
                    message.contract_name = Contract.Field<String>("contract_name");
                    message.contract_type = Contract.Field<String>("contract_type");
                    message.next_status = message.GetNextStatus(Contract.Field<String>("next_status"));
                    message.crm_main_contract_code = Contract.Field<String>("crm_main_contract_code");
                    message.contract_name = Contract.Field<String>("contract_name");
                    message.project_name = Contract.Field<String>("pname").Replace("  "," ");
                    message.manager_in_charge = Contract.Field<String>("PM_email");
                    message.manager_code = Contract.Field<String>("PM_code");
                    message.fm_mail = Contract.Field<String>("FM_email");
                    message.fc_mail = Contract.Field<String>("FC_email");
                    message.manager_code = Contract.Field<String>("PM_code");
                    message.ContractValue = TCV;
                    message.managing_director = Contract.Field<String>("MD_email");
                  
                    try
                  {
                        char[] firstchars = message.project_name.Split(' ').Select(s => s[0]).ToArray();

                        message.project_code = firstchars[0].ToString() + firstchars[1].ToString() + firstchars[2].ToString();
                        message.project_code += "-" + message.project_id;

                    }
                    catch
                    {
                        message.project_code += "INT-" + message.project_id;

                    }

                    message.contract_code = Contract.Field<String>("contract_code");
 
              

                    string stm = @"SELECT  IFNULL(actlegal.code,actlegal_mc.code) as act_legal_code, compo.currency, salesoffice.idf as office_id,compo.crm_component_id,compo.component_name, compo.component_value, proj.pname 
 ,c.title as contract_title,c.id as contract_id, c.code  as contract_code,  IFNULL(nav.nav_server,nav_mc.nav_server) as nav_server, IFNULL(nav.nav_user,nav_mc.nav_user) as nav_user ,
	IFNULL(nav.nav_pass,nav_mc.nav_pass) as nav_pass, IFNULL(nav.nav_company,nav_mc.nav_company) as nav_company,c.seller, 
                salesoffice.offices as salesoffice,categoffice.offices as branch, categoffice.idf as sector_id, 
                IFNULL(actlegal.description,actlegal_mc.description) as legaloffice,
                
                cfi.navision_code,cfi.legal_name, cfi.company_name, cfi.registration_no, cfi.address, cfi.tax_no, cfi.city,IFNULL(cficountry.act_country_code,cficountry.iso3c) as iso3c,
               cast( IFNULL(cfi.crm_finance_client_information_id,0) as decimal) as cfi_id , cfi.last_update, cfi.last_update_by, IFNULL(cfi.validator,cfi_mc.validator) as validator, 
                
                nav.cust_VAT_posting, nav.cust_legal_code, cficountry.calling_code as cust_calling_code, CASE categoffice.idf WHEN 47 then '15' WHEN 42 THEN '02'  WHEN 43 THEN '03' ELSE '99' END as Nav_sector
                , cast(c.vat as decimal) as VAT_Rate, cast(c.wht_rate as decimal) as WHT_rate                
                FROM crm_table_contracts c
                inner join crm_table_projects proj on proj.idproj = c.proj_id
                left join crm_finance_client_information cfi on c.crm_finance_client_information_id = cfi.crm_finance_client_information_id  
                left join crm_list_proposal_contract_data actlegal on actlegal.code = c.country_initial and actlegal.type = 'legal_entity'
                left join eai_navisionws_config nav on actlegal.eai_navision_company = nav.id and nav.isValid = 1
				left join crm_table_contracts mc on mc.id = c.refer_contract_id
              
                left join crm_finance_client_information cfi_mc on mc.crm_finance_client_information_id = cfi_mc.crm_finance_client_information_id  
                left join crm_list_proposal_contract_data actlegal_mc on actlegal_mc.code = mc.country_initial and actlegal_mc.type = 'legal_entity'
                left join eai_navisionws_config nav_mc on actlegal_mc.eai_navision_company = nav_mc.id and nav_mc.isValid = 1
                inner join hr_menu_offices salesoffice on c.office_id = salesoffice.idf
                inner join hr_menu_offices categoffice on c.category_office = categoffice.idf
                left join hr_menu_country cficountry on cfi.country = cficountry.idp
                inner join crm_component compo on compo.contract_id = c.id
                where c.id = '" + CRMContractId + @"' 
                order by compo.crm_component_id asc, c.id asc
                    ";


                    dataAdapter.SelectCommand = new MySqlCommand(stm, connection);
                    DataTable table = new DataTable();
                    dataAdapter.Fill(table);
                    Console.Write("\r"+table.Rows.Count.ToString() + "Components");


                    int CompoIndex = 1;
                   
                    foreach (DataRow ClientToCreate in table.Rows)
                    { 
                        message.act_legal_code = String.IsNullOrEmpty(ClientToCreate.Field<String>("cust_legal_code")) ? ClientToCreate.Field<String>("act_legal_code") :  ClientToCreate.Field<String>("cust_legal_code");

                        message.act_legal = ClientToCreate.Field<String>("nav_company");
                        message.office_id = ClientToCreate.Field<int>("office_id");
                       

                        message.company_no = ClientToCreate.Field<String>("navision_code");
                        message.company_name = ClientToCreate.Field<String>("company_name");
                        message.legal_name = ClientToCreate.Field<String>("legal_name");
                        message.registration_no = ClientToCreate.Field<String>("registration_no");
                        message.tax_no = ClientToCreate.Field<String>("tax_no");
                        message.address = ClientToCreate.Field<String>("address");
                        message.country_code = ClientToCreate.Field<String>("iso3c");
                        
                        message.nav_server = (Properties.Settings.Default.IsTest == "yes") ? Properties.Settings.Default.NavTestServer : ClientToCreate.Field<String>("nav_server");


                        message.nav_user = Properties.Settings.Default.NavUser;
                        message.nav_pass = Properties.Settings.Default.NavPass;


                        message.contract_title = ClientToCreate.Field<String>("contract_title");
                        message.validator_mail = (ClientToCreate.Field<String>("validator") == "") ? "hien.ntt@archetype-group.com" : ClientToCreate.Field<String>("validator");

                        message.bdd_mail = ClientToCreate.Field<String>("seller");
                        try { 
                        message.client_financial_id = ClientToCreate.Field<int>("cfi_id").ToString();
                        } catch { message.client_financial_id = "0"; }


                        if (message.contract_type == "Contract")
                        {
                            message.VAT_posting = ClientToCreate.Field<String>("cust_VAT_posting").ToString();
                            message.sector_id = ClientToCreate.Field<String>("Nav_sector").ToString();
                            message.CountryCallingCode = String.IsNullOrEmpty(ClientToCreate.Field<String>("cust_calling_code")) ? "" : ClientToCreate.Field<String>("cust_calling_code").ToString().PadLeft(3, '0');
                            message.sector_id = ClientToCreate.Field<String>("Nav_sector").ToString();
                        }
                        message.VATRate =  (double)ClientToCreate.Field<decimal>("VAT_Rate"); 
                        message.WHTRate =(double) ClientToCreate.Field<decimal>("WHT_Rate"); 

                        if (ClientSynchro.CheckCompanyCode(message) == "" && message.contract_type == "Contract")
                        {
                           
                            try
                            {

                             
                                if (SyncMode == 0 || SyncMode == 1 || SyncMode == 4 || SyncMode == 5)
                                {
                                    ClientSynchro.ProcessCreation(message);

                                    if (CommunicationMode == 1) ClientSynchro.SendSuccessMail(message);
                              
                                if (ConsoleApp1.Properties.Settings.Default.IsTest != "yes")
                                {
                                    stm = "update crm_finance_client_information set navision_code = '" + message.Navision_Code + "', status = 3 where crm_finance_client_information_id =" + message.client_financial_id;
                                    MySqlCommand cmd = new MySqlCommand(stm, connectionExec);
                                    if (SyncMode == 0 || SyncMode == 1 || SyncMode == 4 || SyncMode == 5) cmd.BeginExecuteNonQuery();
                                }
                                }
                            }
                            catch (Exception e)
                            {
                                Console.Write( "Sending Error message to : " + Properties.Settings.Default.MailRecipients);

                                SendSysErrorMail(e);
                            }
                        }


                            message.ComponentValue = ClientToCreate.Field<string>("component_value");
                        message.ComponentId = ClientToCreate.Field<int>("crm_component_id");
                        message.ComponentCurrency = ClientToCreate.Field<string>("currency");
                        message.CountryIso3c = ClientToCreate.Field<string>("iso3c");
                        try
                        {
                         
                            if (message.contract_type == "Contract" || message.contract_type == "Addendum")
                                {

                            if (SyncMode == 0 || SyncMode == 1 || SyncMode == 4 || SyncMode == 5 || SyncMode == 6) CreateDimValueCode(message);
                         
                            if (SyncMode == 0 || SyncMode == 1 || SyncMode == 4 || SyncMode == 5 || SyncMode == 6) CreateIntranetCode(message);
                                }

                          
                            if (NbOfCompo > 1)
                            {
                                if (CompoIndex == 1)
                                {
                                    message.ComponentCode = message.contract_code;
                                    message.ComponentName = "Contract";


                                    if (SyncMode == 0 || SyncMode == 1 || SyncMode == 4 || SyncMode == 5 || SyncMode == 6) ProcessCreation(message, "main");

                                }

                                if (CompoIndex >= 1)
                                {
                                    
                                    message.ComponentCode = message.contract_code + "-C" + CompoIndex.ToString().PadLeft(2, '0');
                                    message.ComponentName = ClientToCreate.Field<String>("component_name");
                                    if (SyncMode == 0 || SyncMode == 1 || SyncMode == 4 || SyncMode == 5 || SyncMode == 6) ProcessCreation(message, "compo"); //
                                }

                            }
                            else
                            {
                                if (CompoIndex == 1)
                                {
                                   
                                    //update crm component code if success
                                    message.ComponentCode = message.contract_code;
                                    message.ComponentName = "Single Component";
                                    if (SyncMode == 0 || SyncMode == 1 || SyncMode == 4 || SyncMode == 5 || SyncMode == 6) ProcessCreation(message, "main"); //
                                }

                            }

                            if (message.contract_type == "Contract")
                            {

                                if (SyncMode == 0 || SyncMode == 1 || SyncMode == 4 || SyncMode == 5 || SyncMode == 6) AttachDimProject(message);
                            }
                            
                        }
                        catch (Exception e)
                        {
                            Console.Write(e.Message);
                            if (CommunicationMode == 1) SendSysErrorMail(e);
                        }

                        string openTS;

                        openTS = (ConsoleApp1.Properties.Settings.Default.IsTest == "yes") ? Properties.Settings.Default.TSWSTest : Properties.Settings.Default.TSWSLive;

                        try
                        {
                            if (NbOfCompo > 1)
                            {

                                if (CompoIndex >= 1)
                                {

                                    openTS += "?project_name=" + message.project_name;
                                    openTS += "&project_manager=" + message.manager_code;
                                    openTS += "&contract_code=" + message.ComponentCode;

                                    openTS += "&contract_name=" + message.contract_name + "-"+message.ComponentName;

                                    openTS += "&component_code=1-10";
                                    openTS += "&component_name=" + message.ComponentName;
                                    openTS += "&office_id=" + message.office_id;
                                    openTS += "&auth_email=loup.rouan@archetype-group.com&auth_token=1234567890&action=open_virginTS";
                                    

                                    if (SyncMode == 0 || SyncMode == 2 || SyncMode == 5 ) Get(openTS);
                                }

                            }
                            else
                            {
                                if (CompoIndex == 1)
                                {

                                    openTS += "?project_name=" + message.project_name;
                                    openTS += "&project_manager=" + message.manager_code;
                                    openTS += "&contract_code=" + message.contract_code;

                                    openTS += "&contract_name=" + message.contract_name;

                                    openTS += "&component_code=1-10";
                                    openTS += "&component_name=" + message.ComponentName;
                                    openTS += "&office_id=" + message.office_id;
                                    openTS += "&auth_email=loup.rouan@archetype-group.com&auth_token=1234567890&action=open_virginTS";
                                    if (SyncMode == 0 || SyncMode == 2 || SyncMode == 5) Get(openTS);
                                }

                            }



                        }
                        catch (Exception e)
                        {
                            //SendTS ERROR mail
                            SendSysErrorMail(e);
                        }
                        string openEproject;

                       openEproject = (ConsoleApp1.Properties.Settings.Default.IsTest == "yes") ? Properties.Settings.Default.eprojectWSTest : Properties.Settings.Default.eprojectWSLive;


                        openEproject += "?contract_id=" + message.contract_id;
                        openEproject += "&bdd=" + message.bdd_mail;
                        openEproject += "&fm=" + message.validator_mail;
                        openEproject += "&md=" + message.managing_director;
                        openEproject += "&pm=" + message.manager_in_charge;
                    
                        openEproject += "&office_id=" + message.office_id;
                        openEproject += "&auth_email=loup.rouan@archetype-group.com&auth_token=1234567890&action=open_virgineproject";
                        try
                        {
                            if (NbOfCompo > 1)
                            {

                                if (CompoIndex == 1 && message.contract_type == "Contract")
                                {
                                   
                                    if (SyncMode == 0 || SyncMode == 3 || SyncMode == 6) Get(openEproject);
                                }

                            }
                            else
                            {
                                if (CompoIndex == 1 && message.contract_type == "Contract")
                                {
                                    if (SyncMode == 0 || SyncMode == 3 || SyncMode == 6) Get(openEproject);
                                }

                            }



                        }
                        catch (Exception e)
                        {
                            
                            SendSysErrorMail(e);
                        }


                        if (message.contract_type == "Addendum")
                        {
                            string addAddendum; 
                            addAddendum = (ConsoleApp1.Properties.Settings.Default.IsTest == "yes") ? Properties.Settings.Default.eprojectWSTest : Properties.Settings.Default.eprojectWSLive;
                            addAddendum += "?maincontract_code=" + message.crm_main_contract_code;
                            addAddendum += "&addendum_code=" + message.ComponentCode;
                            addAddendum += "&addendum_name=" + message.contract_name ;

                            addAddendum += "&component_name=" +   message.ComponentName;
                            addAddendum += "&value_add=" + message.ComponentValue;
 
                            addAddendum += "&auth_email=loup.rouan@archetype-group.com&auth_token=1234567890&action=add_addendumeproject";

                            if (SyncMode == 0 || SyncMode == 3 || SyncMode == 6) Get(addAddendum);

                        }


                        CompoIndex++;
                    }


                    if (CommunicationMode == 1) SendSuccessMail(message);

                    if (ConsoleApp1.Properties.Settings.Default.IsTest != "yes")
                    {
                      

                        stm = "update crm_table_contracts set validate_status = " + message.next_status + " where id =" + message.contract_id;

                        MySqlCommand cmd3 = new MySqlCommand(stm, connectionExec);
                        if (SyncMode == 0 || SyncMode == 1 || SyncMode == 4 || SyncMode == 5 || SyncMode == 6) cmd3.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception e)
            {
                SendSysErrorMail(e);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
                if (connection != null)
                    connection.Close();
                if (readerExec != null)
                    readerExec.Close();
                if (connectionExec != null)
                    connectionExec.Close();
            }


        }


        static public bool AcceptAllCertifications(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        static string Get(string uri)
        {
            if (ConsoleApp1.Properties.Settings.Default.IsTest == "yes") return "TEST";
                ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(AcceptAllCertifications);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0";
            request.Headers.Add("UserAgent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0");
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Headers.Add("Accept-Language", "en-us,en;q=0.5");
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            request.Headers.Add("Accept-Charset", "ISO-8859-1,utf-8;q=0.7,*;q=0.7");
            request.Method = "GET";
            request.KeepAlive = true; 
            request.ContentType = "text/html";
            request.Referer = "https://nav-2013.archetype-group.com";
            
            request.Headers.Add("Pragma", "no-cache");
            request.Headers.Add("Cache-Control", "no-cache");

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                string status = response.StatusCode.ToString();
                return status;
            }
        }
        static void SendSysErrorMail(Exception e)
        {
            string recipients = ConsoleApp1.Properties.Settings.Default.MailRecipients;
           
            string subject = "[ERROR] Contract Creation for Nav";
            string content = @"Dear Colleague,<br><br>
This mail follow an attempt to automatically create a Contract in our Financial System (Navision). 
<br>Herebelow detailled of the failure:<br><br>
<table>
<tr><td>Error Message </td><td>" + e.Message + @"</td></tr> 
</table>

<br><br>
Best regards,
<br>Information System Team
";
            IntranetMailing.SendMail(subject, content, recipients);
        }

        static void SendSuccessMail(MQMessage message)
        {
            string recipients = ConsoleApp1.Properties.Settings.Default.MailRecipients;
            message.validator_mail = message.validator_mail == "" || message.validator_mail == null ? "loup.rouan@archetype-group.com" : message.validator_mail;

            message.bdd_mail = message.bdd_mail == "" || message.bdd_mail == null ? "loup.rouan@archetype-group.com" : message.bdd_mail;

            message.manager_in_charge = message.manager_in_charge == "" || message.manager_in_charge == null ? "loup.rouan@archetype-group.com" : message.manager_in_charge;

            message.fc_mail = message.fc_mail == "" || message.fc_mail == null ? "loup.rouan@archetype-group.com" : message.fc_mail;

            message.fm_mail = message.fm_mail == "" || message.fm_mail == null ? "loup.rouan@archetype-group.com" : message.fm_mail;


            if (ConsoleApp1.Properties.Settings.Default.IsTest != "yes") recipients += "," + message.validator_mail + "," + message.bdd_mail + "," + message.manager_in_charge + "," + message.fc_mail + "," + message.fm_mail + ",cyrille.dreuillet@archetype-group.com";
            string subject =  "[Success] "+ message.contract_type+ " Creation : " + message.contract_title + " in " + message.act_legal;
 
            var CurrentCulture = new System.Globalization.CultureInfo("en");
            CurrentCulture.NumberFormat.CurrencySymbol = "("+message.ComponentCurrency+") "; 
            string content = @"Dear Colleague,<br><br>
This mail follow a successfull "+message.contract_type+ @" creation in our Financial System (Navision), opening of its Timesheet and E-Project. 

<br><br><B>NEXT STEPS :</b><br><br>

<b>BUDGET </b>: After the transfer report, the PM shall create/manage the budget in E-Project Follow this :  <a href='https://www.archetype-intranet.com/arche_project/budget_planning/management?project_id=" + message.contract_id + @"'>LINK</a> <br> 
2 Options :<br> <li> An estimation has been made at the BD phase, just click the button Import the Budget from BD, review the scope of work and amounts.</li>
<br> <li> No budget made during BD phase, just create a new one, open the scope of work and choose to request inputs from heads of department.</li>
<br>
<b>Timesheets </b>: If your budget is not yet ready but the work already started, the PM (" + message.manager_in_charge + @") and the MD (" + message.managing_director + @")  have to open phases in the Timesheet management (follow the <a href='https://www.archetype-intranet.com/timesheet/project_status.php'>LINK</a>)
<br>
<b>E-PROJECT :</b> The E-Project has been opened but the transfer report is still to be completed by BD and PM following this link : <a href='https://www.archetype-intranet.com/arche_project/transfer_report/detail?project_id=" + message.contract_id + @"'>LINK</a>
<br>
<b>NAVISION :</b> Finance still have to validate/control the job in Navision.
<br>
<b>The deadline of the transfer Meeting has been set to 1 week following this email and a remider will be sent to PM / BD / MD, one day before.</b>
<br><br>

<br>Contract details :<br><br>
<table>

<tr><td><b>Customer Generic Name</b></td><td>" + message.company_name + @"</td></tr>
<tr><td><b>Customer Legal Name</b></td><td>" + message.legal_name + @"</td></tr>
<tr><td><b>Customer Nav Code</b></td><td>" + message.company_no + @"</td></tr>
<tr><td><b>Customer VAT code</b></td><td>" + message.tax_no + @"</td></tr>
<tr><td><b>Customer Reg. Code</b></td><td>" + message.registration_no + @"</td></tr>

<tr><td><b>Project </b></td><td>" + message.project_name + @"</td></tr>
<tr><td><b>Contract Name </b></td><td>" + message.contract_title + @"</td></tr> 
<tr><td><b>Contract Code </b></td><td>" + message.contract_code + @"</td></tr> 
<tr><td><b>Total Contract Value </b></td><td>" + message.ContractValue.ToString("C", CurrentCulture) + @"</td></tr> 
<tr><td><b>Manager In Charge </b></td><td>" + message.manager_in_charge + @"</td></tr> 
<tr><td><b>Financial info Validator </b></td><td>" + message.validator_mail + @"</td></tr> 

</table>
<br><br>
For any question, please contact dev-support@archetype-group.com
<br><br>
Best regards,
<br>Information System Team
";

 if (message.contract_type == "Addendum") { 
            content = @"Dear Colleague,<br><br>
This mail follow a successfull " + message.contract_type + @" creation in our Financial System (Navision), opening of its Timesheet. 

<br><br><B>NEXT STEPS :</b><br><br>

<b>BUDGET </b>: Operation team still have to prepare a budget, you can go in the Eproject of the main contract.
<br>
<b>TS </b>: If your budget is not yet ready and the work already started,  the PM (" + message.manager_in_charge + @") and the MD (" + message.managing_director + @")  have to open phases in the Timesheet management (follow the <a href='https://www.archetype-intranet.com/timesheet/project_status.php'>LINK</a>)
<br>
<b>E-PROJECT :</b> Operations are already working with the main contract EProject.
<br>
<b>NAVISION :</b> Finance still have to validate/control the job in Navision.
<br>
 

<br>Contract details :<br><br>
<table>

<tr><td><b>Customer Infos</b></td><td>Already in the system</td></tr> 

<tr><td><b>Project </b></td><td>" + message.project_name + @"</td></tr>
<tr><td><b>Main contract Code</b></td><td>" + message.crm_main_contract_code + @"</td></tr> 
<tr><td><b>Addendum Name </b></td><td>" + message.contract_title + @"</td></tr> 
<tr><td><b>Contract Code </b></td><td>" + message.contract_code + @"</td></tr> 
<tr><td><b>Total Contract Value </b></td><td>" + message.ContractValue.ToString("C", CurrentCulture) + @"</td></tr> 
<tr><td><b>Manager In Charge </b></td><td>" + message.manager_in_charge + @"</td></tr> 
<tr><td><b>Financial info Validator </b></td><td>" + message.validator_mail + @"</td></tr> 

</table>
<br><br>
For any question, please contact dev-support@archetype-group.com
<br><br>
Best regards,
<br>Information System Team
";

            }
            IntranetMailing.SendMail(subject, content, recipients);
        }

        

        static int CreateDimValueCode(MQMessage message)
        {

            
            DimValueWS.DimValueWS_Service WSDimValue = new DimValueWS.DimValueWS_Service();
            WSDimValue.Url = "http://" + message.nav_server + "/WS/" + message.act_legal.Replace(" ", "%20") + "/Page/DimValueWS";
            WSDimValue.Credentials = new NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");
            DimValueWS.DimValueWS DimValueProject = new DimValueWS.DimValueWS();
           

            DimValueWS.DimValueWS DimValueProject2 = new DimValueWS.DimValueWS();

            try
            {
                DimValueProject2 = WSDimValue.Read("PROJECT", message.project_code.ToString());
            }
            catch (Exception e)
            {
                throw e;
            }


            try
            {
                if (DimValueProject2.Name != null)
                {
                    message.project_name = DimValueProject2.Name;
               
                }
            }
            catch (Exception e)
            {
            }


            DimValueProject.Dimension_Code = "PROJECT";
            DimValueProject.Code = message.project_code;
            DimValueProject.Name = message.project_name;


            try
            {
                WSDimValue.Create(ref DimValueProject);
            } catch (Exception e)
            { 
                if (!e.Message.Contains("already exist")) throw e;
            }
            return 1;
        }

        static int CreateIntranetCode(MQMessage message)
            {

           
            IntranetInsertNAVWS.IntranetInsertNAVWS WSIntCode = new IntranetInsertNAVWS.IntranetInsertNAVWS();
            WSIntCode.Url = "http://" + message.nav_server + "/WS/" + message.act_legal.Replace(" ", "%20") + "/Codeunit/IntranetInsertNAVWS";
            WSIntCode.Credentials = new NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");



                try { 
                
            WSIntCode.InsertIntoIntranet(18, message.project_code, message.project_name, message.contract_code);

            }
            catch (Exception e)
            {
               
                if (!e.Message.Contains("already exist")) throw e;

            }
            return 1;
            }

      
        public class DummyValues
        {
            public string JobNo { get; set; }
            public string Description { get; set; }
            public DateTime PlanningDate { get; set; }
            public DateTime PlanningDeliveryDate { get; set; }
            public int LineType { get; set; }  
            public decimal LineTotal { get; set; }
            public bool IsFirstLine { get; set; }
        }

        static int CreatePaymentSchedule(MQMessage message)
        {
            
            DateTime nullDate = new DateTime();
            
          
            List<DummyValues> _items = new List<DummyValues>();
            _items.Add(new DummyValues { JobNo = "C-SGN-EXT-17-011-B00", Description = "First", LineType = 1, LineTotal = 1000, PlanningDate = nullDate, PlanningDeliveryDate = nullDate, IsFirstLine = true });
            _items.Add(new DummyValues { JobNo = "C-SGN-EXT-17-011-B00", Description = "Second", LineType = 1, LineTotal = 4500, PlanningDate = nullDate, PlanningDeliveryDate = nullDate, IsFirstLine = false });
            _items.Add(new DummyValues { JobNo = "C-SGN-EXT-17-011-B00", Description = "Thrid", LineType = 1, LineTotal = 7900, PlanningDate = nullDate, PlanningDeliveryDate = nullDate, IsFirstLine = false });
             
            PaySchIntegration.PaySchIntegration PSIntegration = new PaySchIntegration.PaySchIntegration();
            PSIntegration.Url = "http://" + message.nav_server + "/WS/" + message.act_legal.Replace(" ", "%20") + "/Codeunit/PaySchIntegration";
            PSIntegration.Credentials = new NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");
            
            try
            {

                foreach (var item in _items)
                {
                    if (PSIntegration.CreateJobTaskLine(item.JobNo, item.Description, item.LineType, item.PlanningDate, item.PlanningDeliveryDate, item.LineTotal, item.IsFirstLine))
                    {
                        Console.WriteLine("Sucess - payment schedule.");
                    }
                    else
                    {
                        Console.WriteLine("Fail - payment schedule.");

                    }
                }
            }
            catch (Exception e)
            {
           
            }
            return 1;
        }
      
        static int AttachDimProject(MQMessage message)
        {

         
            DevDimWS.DefDimWS CUDefDim = new DefDimWS();
            CUDefDim.Url = "http://" + message.nav_server + "/WS/" + message.act_legal.Replace(" ", "%20") + "/Codeunit/DefDimWS";
            CUDefDim.Credentials = new NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");                                                //ROLLOUT the new CU and expose it as a webservice in every server.
            CUDefDim.AddDimension(167, "PROJECT", message.project_code, message.project_name, message.contract_code);

            return 1;
        }

       

        static int ProcessCreation(MQMessage message, String Type = "main") {

             
            int BypassReadOnlyAfterCreation = 0; 
            Contracts_Service WSContract = new ContractsWS.Contracts_Service();
            WSContract.Url = "http://"+message.nav_server+"/WS/" + message.act_legal.Replace(" ","%20") + "/Page/Contracts";
            WSContract.Credentials = new NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");
            
            message.company_no = message.Navision_Code;
          
            ContractsWS.Contracts newjob = new ContractsWS.Contracts();

           
            if (message.ComponentCurrency == "EUR" || message.ComponentCurrency == "USD") newjob.Currency_Code = message.ComponentCurrency;
    
            newjob.WIP_Method = "POC DS";
            newjob.Overide_for_TS_WIP_method = "POC DS";

            if (Type == "main")
            {
                newjob.No = message.contract_code;
                newjob.Contract_Code = message.contract_code;
                newjob.Component_Name = message.ComponentName;
                 
             
            }
            else
            {
                newjob.No = message.ComponentCode;
                newjob.Contract_Code = message.ComponentCode;
                newjob.Component_Name = message.ComponentName;
            }

         

        
           
            newjob.Description = message.contract_name; 


          

     ContractsWS.Contracts MainJob = new ContractsWS.Contracts();
            if (message.contract_type == "Addendum") {

                try {
               
                    MainJob  = WSContract.Read(message.crm_main_contract_code);
                    newjob.Description_2 = MainJob.Description_2.Replace("  ", " ");
                }
                catch { }
               
            }
            else newjob.Description_2 = message.project_name.Replace("  "," ");



            try
            { 
            WSContract.Create(ref newjob);

            }
            catch (Exception e)
            {
    
                if (!e.Message.Contains("already exist"))
                {
                    BypassReadOnlyAfterCreation = 1;
                    throw e; 
                }
            }

      
            ContractsWS.Contracts jobUpdate = new ContractsWS.Contracts();
            if (Type == "main")
            {
              
                jobUpdate = WSContract.Read(message.contract_code);
              
                if ((BypassReadOnlyAfterCreation == 0 && jobUpdate.Contract_Value == 0)) jobUpdate.Contract_Value = Convert.ToDecimal(message.ContractValue);


                try
                {
                    jobUpdate.VAT = (decimal)message.VATRate;
                }
                catch { }
                try
                {
                    jobUpdate.WHT = (decimal)message.WHTRate;
                }
                catch { }

                if (message.contract_type == "Addendum" ) {

                    if (BypassReadOnlyAfterCreation == 0) jobUpdate.Contract_Type = (Contract_Type)Enum.Parse(typeof(Contract_Type), "Addendum");
                    jobUpdate.Primary_Contract_No = message.crm_main_contract_code;
                    jobUpdate.Main_Contract_No = message.crm_main_contract_code;
                
                        jobUpdate.TS_Status = (decimal)1;
                   

                }
                else {
                    
                    if (BypassReadOnlyAfterCreation == 0) jobUpdate.Contract_Type = (Contract_Type)Enum.Parse(typeof(Contract_Type), "Primary");
                    if (message.ComponentName == "Single Component") jobUpdate.TS_Status = (decimal)1;

                }
                
            } else
            {
              
                jobUpdate = WSContract.Read(message.ComponentCode);
              
                jobUpdate.Contract_Type = (Contract_Type)Enum.Parse(typeof(Contract_Type), "Component");
                jobUpdate.Main_Contract_No = message.contract_code;
               
             
                jobUpdate.Primary_Contract_No = message.contract_code;

                jobUpdate.TS_Status = (decimal)1;
                if ( (BypassReadOnlyAfterCreation == 0 && jobUpdate.Contract_Value == 0)) jobUpdate.Contract_Value =  Convert.ToDecimal(message.ComponentValue);

            }
           

            jobUpdate.Legal_entities =  (Legal_entities) Enum.Parse(typeof(Legal_entities), "_blank_"); 
            jobUpdate.Budget_Type = Budget_Type.DS;  

            jobUpdate.Budget_Currnecy = "";  


            jobUpdate.Bill_to_Customer_No = message.company_no;
            if (message.contract_type == "Addendum")
            {
                try
                {
                    jobUpdate.Bill_to_Customer_No = MainJob.Bill_to_Customer_No;
                }
                catch { } 
            }
            try
            {
                jobUpdate.VAT = (decimal)message.VATRate;
            }
            catch { }
            try
            {
                jobUpdate.WHT = (decimal)message.WHTRate;
            }
            catch { }
            try
            {
                WSContract.Update(ref jobUpdate);

            }
            catch (Exception e)
            {
             
                if (!e.Message.Contains("already exist") && !e.Message.Contains("Primary"))   throw e; // we ignore if project already exist, it will update intranet accordingly to avoid retaking it.
            }
           

            return 1;
            
        }
        
    }
}
 