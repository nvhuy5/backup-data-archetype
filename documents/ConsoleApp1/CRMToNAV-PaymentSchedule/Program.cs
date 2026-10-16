using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;

// TODO : verify that the table in Intranet is well configured for LIVE
// TODO remove Myanmar DB as it is not in our network at the moment
// DONE : VAT Posting group is VAT for Vietnam not TRADE
// DONE : check registration number if not null check it in existing client
// DONE : Check Tax num if not null test it as existing client
// DONE : implement country code of the signing entity.


//Requierments :
// the server hosting the app should have a network link to archetype-intranet.com on port 3306
// webservices named Dimensions and Contracts for the entity Job Card should be created in all Nav instance targetted by the tool 
// the configuration of Navision instances should be managed in a MySQL table of the intranet server  eai_navisionws_config
// the tool should be scheduled every XX minutes using windows schedule of the server hosting the app.

// Explanation:
// this tool is connecting to the CRM database to list pending client to create in Navision
// A client in intranet is created when the BDD convert a proposal to contract, a request to validate by a financial controller 
// is made and if he approve, the client is ready to be created (status = 2).
// a list of mendatory information are pulled out by the SQL query and it allow to direct the customer creation to the good
// Navision entity : base on the CRM Field "ACT legal entity" (as the CRM is not clear in its design, the link is made using the
// country code and the type of relation (legal entity) ot a table named crm_list_proposal_contract_data
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
    
    using ConsoleApp1.DevDimWS;
    using ConsoleApp1.IntranetCodes;
    using System.IO;

    class Program
    {
         
        static void Main(string[] args)
        {
             
            // read the intranet DB to check Client to create.
            string connectionString = ConsoleApp1.Properties.Settings.Default.IntranetSQL;
            MySqlConnection connection = null;
            MySqlDataReader reader = null;
            
            try
            {
                connection = new MySqlConnection(connectionString);
                connection.Open();

                //To Synchronize a new contract we need it and we need to check if the customer is existing.
                //status = 2 for client to import; 1 for client in the validation process
                //Map the office categ ID to construct the customer code with NAV categories RE = 15 ; Industry = 02, Energy = 03

                // 1 - Check which contract is ready for synchro.
                //Condition for a contract to be synchronized.
                // Its clients is already in Nav. Check crm_financial_client_info, status = 3 = created.
                // The contract don't exist already
                // its contract information are validated in Intranet (status = 2 of crm_table_contract)
                //TODO BEFORE LIVE : add check on sync and status of client/contract.
                string CheckContractToSync = @"SELECT  c.proj_id as project_id, c.refer_contract_id as main_contract_id, mc.code as crm_main_contract_code, c.id as contract_id, CASE WHEN c.contract_type = 'A' THEN 'Addendum' WHEN c.contract_type = 'C' THEN 'Contract' END as Contract_type ,c.title as contract_name,c.manager_in_charge as PM_email, c.seller,pm.code as PM_code, IFNULL(IFNULL(IFNULL(md.email,md_2.email),salesoffice.managing_director) ,'loup.rouan@archetype-group.com') as MD_email, c.id as contract_id, proj.pname, salesoffice.idf as office_id, c.code  as contract_code, count(compo.crm_component_id) as CompoNb, sum(compo.component_value) as TCV,
IFNULL(IFNULL(fm.email,fm_2.email), 'loup.rouan@archetype-group.com') as FM_email,
IFNULL(IFNULL(fc.email,fc_2.email) ,'loup.rouan@archetype-group.com') as FC_email
                FROM crm_table_contracts c
                inner join hr_table_main pm on pm.arche_email = c.manager_in_charge
                left join crm_table_contracts mc on mc.id = c.refer_contract_id
              
                inner join crm_table_projects proj on proj.idproj = c.proj_id
                inner join crm_finance_client_information cfi on c.crm_finance_client_information_id = cfi.crm_finance_client_information_id  
                inner join crm_list_proposal_contract_data actlegal on actlegal.code = c.country_initial and actlegal.type = 'legal_entity'
                inner join eai_navisionws_config nav on actlegal.eai_navision_company = nav.id and nav.isValid = 1
                inner join hr_menu_offices salesoffice on c.office_id = salesoffice.idf
                left join prm_purchase_order_position md on md.office_code = salesoffice.office_code and md.position= 'Managing director'
                left join prm_purchase_order_position md_2 on md.country_code = salesoffice.office_code and md_2.position= 'Managing director'
                left join prm_purchase_order_position fc on fc.office_code = salesoffice.office_code and fc.position= 'Finance controller'
                left join prm_purchase_order_position fc_2 on fc.country_code = salesoffice.office_code and fc_2.position= 'Finance controller'
                left join prm_purchase_order_position fm on fm.office_code = salesoffice.office_code and fm.position= 'Finance manager'
                left join prm_purchase_order_position fm_2 on fm.country_code = salesoffice.office_code and fm_2.position= 'Finance manager' 
                                
                inner join hr_menu_offices categoffice on c.category_office = categoffice.idf
                left join hr_menu_country cficountry on cfi.country = cficountry.idp
                inner join crm_component compo on compo.contract_id = c.id
                where c.validate_status = 2
                group by c.id, c.code, proj.pname
";
                MySqlDataAdapter dataAdapter = new MySqlDataAdapter();
                dataAdapter.SelectCommand = new MySqlCommand(CheckContractToSync, connection);
                DataTable ContractToCreate = new DataTable();
                dataAdapter.Fill(ContractToCreate);
               

                foreach (DataRow Contract in ContractToCreate.Rows)
                {
                    int CRMContractId = Contract.Field<int>("contract_id");
                    int NbOfCompo = (int)Contract.Field<Int64>("CompoNb"); 
                    double TCV = Contract.Field<double>("TCV");
                    // 2 - process all component of a contract. 
                    // If the contract only have one component, create 1 main contract.
                    // If the contract have many compo, create 1 parent contract, x child.

                    MQMessage message = new MQMessage();
                    //HERE data that have to be always processed once if many component
                    //Generate project Code 

                    message.project_id = Contract.Field<int>("project_id");
                    message.contract_name = Contract.Field<String>("contract_name");
                    message.contract_type = Contract.Field<String>("contract_type");
                    message.crm_main_contract_code = Contract.Field<String>("crm_main_contract_code");
                    message.contract_name = Contract.Field<String>("contract_name");
                    message.project_name =  Contract.Field<String>("pname");
                    message.manager_in_charge =   Contract.Field<String>("PM_email");
                    message.manager_code =  Contract.Field<String>("PM_code");
                    message.fm_mail = Contract.Field<String>("FM_email");
                    message.fc_mail = Contract.Field<String>("FC_email");
                    message.manager_code = Contract.Field<String>("PM_code");
                     message.ContractValue = TCV;
                    //todo add fc
                    message.managing_director = Contract.Field<String>("MD_email");
                    //try to do 3 first char + intranet project_id or INT for INTRANET + Project Id
                    try
                    {
                        char[] firstchars = message.project_name.Split(' ').Select(s => s[0]).ToArray();

                        message.project_code = firstchars[0].ToString() + firstchars[1].ToString() + firstchars[2].ToString();
                        message.project_code += "-" + message.project_id;

                    }
                    catch {
                        message.project_code += "INT-" + message.project_id;

                    }

                    message.contract_code = Contract.Field<String>("contract_code");
                


                    //Process each component (minimu 1 compo)

                    string stm = @"SELECT actlegal.code as act_legal_code, compo.currency, salesoffice.idf as office_id,compo.crm_component_id,compo.component_name, compo.component_value, proj.pname  ,c.title as contract_title,c.id as contract_id, c.code  as contract_code, nav.nav_server, nav.nav_user, nav.nav_pass, nav.nav_company,c.seller, 
                salesoffice.offices as salesoffice,categoffice.offices as branch, categoffice.idf as sector_id, actlegal.description as legaloffice,
                cfi.navision_code,cfi.legal_name, cfi.company_name, cfi.registration_no, cfi.address, cfi.tax_no, cfi.city,IFNULL(cficountry.act_country_code,cficountry.iso3c) as iso3c,
                cfi.crm_finance_client_information_id as cfi_id , cfi.last_update, cfi.last_update_by, cfi.validator, nav.cust_VAT_posting, cficountry.calling_code as cust_calling_code, CASE categoffice.idf WHEN 47 then '15' WHEN 42 THEN '02'  WHEN 43 THEN '03' ELSE '99' END as Nav_sector
                FROM crm_table_contracts c
                inner join crm_table_projects proj on proj.idproj = c.proj_id
                inner join crm_finance_client_information cfi on c.crm_finance_client_information_id = cfi.crm_finance_client_information_id  
                inner join crm_list_proposal_contract_data actlegal on actlegal.code = c.country_initial and actlegal.type = 'legal_entity'
                inner join eai_navisionws_config nav on actlegal.eai_navision_company = nav.id and nav.isValid = 1
                inner join hr_menu_offices salesoffice on c.office_id = salesoffice.idf
                inner join hr_menu_offices categoffice on c.category_office = categoffice.idf
                left join hr_menu_country cficountry on cfi.country = cficountry.idp
                inner join crm_component compo on compo.contract_id = c.id
                where c.id = '" + CRMContractId + @"'
                order by compo.crm_component_id asc, c.id asc
                    ";
                    //C-SGN-EXT-17-016-B00 working vn ok


                    dataAdapter.SelectCommand = new MySqlCommand(stm, connection);
                    DataTable table = new DataTable();
                    dataAdapter.Fill(table);
                    Console.Write(table.Rows.Count);


                    int CompoIndex = 1;

                    foreach (DataRow ClientToCreate in table.Rows)
                    {  // populate Message in case we plug rabbitMQ in the future


                        message.act_legal_code = ClientToCreate.Field<String>("act_legal_code");
                        message.act_legal = ClientToCreate.Field<String>("nav_company");
                        message.office_id = ClientToCreate.Field<int>("office_id");
                        // now managed by client check:  
                        message.company_no = ClientToCreate.Field<String>("navision_code");
                        message.company_name = ClientToCreate.Field<String>("company_name");
                        message.legal_name = ClientToCreate.Field<String>("legal_name");
                        message.registration_no = ClientToCreate.Field<String>("registration_no");
                        message.tax_no = ClientToCreate.Field<String>("tax_no");
                        message.address = ClientToCreate.Field<String>("address");
                        message.country_code = ClientToCreate.Field<String>("iso3c");
                        //override the destination server in case of test env variable
                        message.nav_server = (Properties.Settings.Default.IsTest == "yes") ? Properties.Settings.Default.NavTestServer : ClientToCreate.Field<String>("nav_server");
                        
                   
                        message.nav_user = ClientToCreate.Field<String>("nav_user");
                        message.nav_pass = ClientToCreate.Field<String>("nav_pass");
                       // message.contract_code = ClientToCreate.Field<String>("contract_code"); //test
                        message.contract_title = ClientToCreate.Field<String>("contract_title");
                        message.contract_id = ClientToCreate.Field<int>("contract_id").ToString();
                        message.validator_mail = ClientToCreate.Field<String>("validator");

                        message.bdd_mail = ClientToCreate.Field<String>("seller");
                        message.client_financial_id = ClientToCreate.Field<int>("cfi_id").ToString();
                        message.VAT_posting = ClientToCreate.Field<String>("cust_VAT_posting").ToString();
                        //TODO process in case of null data from SQL
                        //message.CountryCallingCode = ClientToCreate.Field<String>("cust_calling_code").ToString().PadLeft(3,'0'); 
                        message.sector_id = ClientToCreate.Field<String>("Nav_sector").ToString();
                        message.CountryCallingCode = (ClientToCreate.Field<String>("cust_calling_code").ToString() == null) ? "" : ClientToCreate.Field<String>("cust_calling_code").ToString().PadLeft(3, '0');
                        message.sector_id = ClientToCreate.Field<String>("Nav_sector").ToString();

                         
                        if (ClientSynchro.CheckCompanyCode(message) == "")
                        {
                            //check if client exist
                            try
                            {

                                //check if client exist if yes, it set company No for the whole loop 
                                ClientSynchro.ProcessCreation(message);
                                ClientSynchro.SendSuccessMail(message);

                                if (ConsoleApp1.Properties.Settings.Default.IsTest != "yes")
                                {
                                    stm = "update crm_finance_client_information set navision_code = '" + message.Navision_Code + "', status = 3 where crm_finance_client_information_id =" + message.client_financial_id;
                                    MySqlCommand cmd = new MySqlCommand(stm, connection);
                                    cmd.BeginExecuteNonQuery();
                                }
                            }
                            catch (Exception e)
                            {
                                SendErrorMail(message, e);
                            }
                        } 

                        //Contract Info

                        //Here Special process if 1 compo of many.
                       
                        message.ComponentValue = ClientToCreate.Field<string>("component_value");
                        message.ComponentId = ClientToCreate.Field<int>("crm_component_id");
                        message.ComponentCurrency = ClientToCreate.Field<string>("currency");
                        message.CountryIso3c = ClientToCreate.Field<string>("iso3c");
                        try
                        {
                            //First Create Dimenson Project if it is not an addendum  

                            //Second Create Project Code in Intranet Code Table (used to link TS with intranet) if it is not an addendum  

                            //third create the contract : if no component 1 main contract
                            //    if x component, create a master job and X child of this job
                            //if type of contract is Addendum, Intranet should give the main contract code
                            //    If addendum is linked to a child compo in CRM from the main contract, should like the addend to the child compo
                            //    If addendum is linked to the main contract or to all compo, should like it to the main contract.
                            //if from eproject we detect a subcontract validated/signed, we should get the main contract info of the CRM and a parent and 1 child as subcontract inside the Nav of the provider (add project dim/code dim as well).

                            CreateDimValueCode(message);
                            CreateIntranetCode(message);
                            // here we create the main contract if we have multi childs.
                            if (NbOfCompo > 1)
                            {
                                if (CompoIndex == 1)
                                {
                                    //todo put parent as main
                                    message.ComponentCode = message.contract_code;
                                    message.ComponentName = "Contract";
                                    ProcessCreation(message, "main");

                                }

                                if (CompoIndex >= 1 )
                                {
                                    //Todo link to parent
                                    //update crm component code if success
                                    message.ComponentCode = message.contract_code + "-C" + CompoIndex.ToString().PadLeft(2, '0');
                                    message.ComponentName = ClientToCreate.Field<String>("component_name");
                                    ProcessCreation(message, "compo"); //
                                }

                            }
                            else {
                                if (CompoIndex == 1)
                                {
                                    //Todo link to parent
                                    //update crm component code if success
                                    message.ComponentCode = message.contract_code;
                                    message.ComponentName = "Single Component";
                                    ProcessCreation(message, "main"); //
                                }

                            }

                            
                        
                            AttachDimProject(message);

                            //[S-43][T-45][Dayan.P][START]
                           // CreatePaymentSchedule(message);
                            //[S-43][T-45][Dayan.P][END]

                            
                        }
                        catch (Exception e)
                        {
                            SendErrorMail(message, e);
                        }
                         
                        string openTS;

                        openTS = (ConsoleApp1.Properties.Settings.Default.IsTest == "yes") ? Properties.Settings.Default.TSWSTest : Properties.Settings.Default.TSWSLive;

                        openTS += "?project_name="+message.project_name;
                        openTS += "&project_manager=" + message.manager_code;
                        openTS += "&contract_code=" + message.contract_code;
                  
                        openTS += "&contract_name=" + message.contract_name;

                        openTS += "&component_code=" + message.ComponentCode;
                        openTS += "&component_name=" + message.company_name;
                        openTS += "&office_id=" + message.office_id;
                        openTS += "&auth_email=loup.rouan@archetype-group.com&auth_token=1234567890&action=open_virginTS";
                        try {
                            if (NbOfCompo > 1)
                            {
                           
                                if (CompoIndex >= 1)
                                {
                                    Get(openTS);
                                }

                            }
                            else
                            {
                                if (CompoIndex == 1)
                                {
                                    Get(openTS);
                                }

                            }



                            }
                        catch (Exception e)
                        {
                            //SendTS ERROR mail
                            SendSysErrorMail( e);
                        }
                        string openEproject;
                
                        openEproject = (ConsoleApp1.Properties.Settings.Default.IsTest == "yes") ? Properties.Settings.Default.eprojectWSTest : Properties.Settings.Default.eprojectWSLive;
                    
                          
                        openEproject += "?contract_id=" + message.contract_id; 
                        openEproject += "&bdd=" + message.bdd_mail;
                        openEproject += "&fm=" + message.validator_mail;
                        openEproject += "&md=" + message.managing_director;
                        openEproject += "&pm=" + message.manager_in_charge;
                        //check opening eproj with only email
                        //todo add pm email in request 
                        openEproject += "&office_id=" + message.office_id;
                        openEproject += "&auth_email=loup.rouan@archetype-group.com&auth_token=1234567890&action=open_virgineproject";
                        try
                        {
                            if (NbOfCompo > 1)
                            {

                                if (CompoIndex == 1 && message.contract_type == "Contract")
                                {
                                     Get(openEproject);
                                }

                            }
                            else
                            {
                                if (CompoIndex == 1 && message.contract_type == "Contract")
                                {
                                    Get(openEproject);
                                }

                            }



                        }
                        catch (Exception e)
                        {
                            //Sendepro ERROR mail
                            SendSysErrorMail( e);
                        }
                        CompoIndex++;
                    }


                    SendSuccessMail(message);

                    if (ConsoleApp1.Properties.Settings.Default.IsTest != "yes")
                    {
                        //TODO update contract table.
                        stm = "update crm_table_contracts set validate_status = 3 where id =" + message.contract_id;

                        MySqlCommand cmd3 = new MySqlCommand(stm, connection);
                        cmd3.BeginExecuteNonQuery();

                    }
                }
            }
            catch(Exception e)
            {
            SendSysErrorMail(e);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
                if (connection != null)
                    connection.Close();
            }


        }

        static public bool AcceptAllCertifications(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        static string Get(string uri)
        {
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

        static void SendErrorMail(MQMessage message, Exception e)
            {
            string recipients = ConsoleApp1.Properties.Settings.Default.MailRecipients ;
            if ( ConsoleApp1.Properties.Settings.Default.IsTest != "yes") recipients += "," + message.validator_mail + "," + message.bdd_mail;
            string subject = "[FAILED] Contract Creation for Nav : " + message.act_legal;
            string content = @"Dear Colleague,<br><br>
This mail follow an attempt to automatically create a contract in our Financial System (Navision). 
<br>Herebelow detailled of the failure:<br><br>
<table>
<tr><td><b>Customer Legal Name</b></td><td>"+message.legal_name+ @"</td></tr>
<tr><td><b>Customer VAT code</b></td><td>" + message.tax_no + @"</td></tr>
<tr><td><b>Customer Reg. Code</b></td><td>" + message.registration_no + @"</td></tr>
<tr><td><b>Error Message </b></td><td>" + e.Message + @"</td></tr> 
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
            IntranetMailing.SendMail(subject,content,recipients);
            }

        static void SendSuccessMail(MQMessage message)
        {
            string recipients = ConsoleApp1.Properties.Settings.Default.MailRecipients;
            if (ConsoleApp1.Properties.Settings.Default.IsTest != "yes") recipients += "," + message.validator_mail + "," + message.bdd_mail + "," + message.manager_in_charge + "," + message.fc_mail + "," + message.fm_mail;
            string subject = "[Success] Contract Creation Automation : " + message.act_legal;
            string content = @"Dear Colleague,<br><br>
This mail follow a successfull Contract creation in our Financial System (Navision), opening of its Timesheet and E-Project. 

<B>NEXT STEPS :</b><br><br>

<b>BUDGET </b>: Operation team still have to prepare a budget. (until the release of our new budget tool in E-Project)
<br>
<b>TS </b>: If your budget is not yet ready and the work already started,  the PM (" + message.manager_in_charge + @") and the MD (" + message.managing_director + @")  have to open phases in the Timesheet management (follow the LINK)
<br>
<b>E-PROJECT :</b> The E-Project has been opened but the transfer report is still to be completed by BD and PM following this link : <a href='https://www.archetype-intranet.com/arche_project/transfer_report/detail?project_id=" + message.contract_id + @"'>LINK</a>
<br>
<b>NAVISION :</b> Finance still have to validate/control the job in Navision.
<br>
<b>The deadline of the transfer Meeting has been set to 1 week following this email and a remider will be sent to PM / BD / MD, one day before.</b>
<br><br>

<br>Contract details :<br><br>
<table>
<tr><td><b>Customer Legal Name</b></td><td>" + message.legal_name + @"</td></tr>
<tr><td><b>Customer Nav Code</b></td><td>" + message.company_no + @"</td></tr>
<tr><td><b>Customer VAT code</b></td><td>" + message.tax_no + @"</td></tr>
<tr><td><b>Customer Reg. Code</b></td><td>" + message.registration_no + @"</td></tr>

<tr><td><b>Project </b></td><td>" + message.project_name + @"</td></tr>
 
<tr><td><b>Contract Name </b></td><td>" + message.contract_title + @"</td></tr> 
<tr><td><b>Contract Code </b></td><td>" + message.contract_code + @"</td></tr> 
</table>
<br><br>
For any question, please contact dev-support@archetype-group.com
<br><br>
Best regards,
<br>Information System Team
";
            IntranetMailing.SendMail(subject, content, recipients);
        }

        

        static int CreateDimValueCode(MQMessage message)
        {

            //add the project name before contract 
            DimValueWS.DimValueWS_Service WSDimValue = new DimValueWS.DimValueWS_Service();
            WSDimValue.Url = "http://" + message.nav_server + "/WS/" + message.act_legal.Replace(" ", "%20") + "/Page/DimValueWS";
            WSDimValue.Credentials = new NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");
            DimValueWS.DimValueWS DimValueProject = new DimValueWS.DimValueWS();
            DimValueProject.Dimension_Code = "PROJECT";
            DimValueProject.Code = message.project_code;
            DimValueProject.Name = message.project_name;
            try
            {
                WSDimValue.Create(ref DimValueProject);
            } catch (Exception e)
            { //we ignore we element already exist
                if (!e.Message.Contains("already exist")) throw e;
            }
            return 1;
        }

        static int CreateIntranetCode(MQMessage message)
            {

            // WS Contract Creation
            IntranetInsertNAVWS.IntranetInsertNAVWS WSIntCode = new IntranetInsertNAVWS.IntranetInsertNAVWS();
            WSIntCode.Url = "http://" + message.nav_server + "/WS/" + message.act_legal.Replace(" ", "%20") + "/Codeunit/IntranetInsertNAVWS";
            WSIntCode.Credentials = new NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");
        
            try {
                //18 = dataset type PN for ProjectNumber
            WSIntCode.InsertIntoIntranet(18, message.project_code, message.project_name, message.contract_code);

            }
            catch (Exception e)
            {
                if (!e.Message.Contains("already exist")) throw e;

            }
            return 1;
            }

        //[S-43][T-45][Dayan.P][START]
        public class DummyValues
        {
            public string JobNo { get; set; }
            public string Description { get; set; }
            public DateTime PlanningDate { get; set; }
            public DateTime PlanningDeliveryDate { get; set; }
            public int LineType { get; set; } //1=Schedule, 2=Contract, 3=Both Schedule and Contract
            public decimal LineTotal { get; set; }
            public bool IsFirstLine { get; set; }
        }

        static int CreatePaymentSchedule(MQMessage message)
        {
            //Date time stuffs
            //Way to insert date //DateTime value = new DateTime(2017, 1, 18);
            //Way to insert null date
            DateTime nullDate = new DateTime();
            
            //Dummy List of array data
            List<DummyValues> _items = new List<DummyValues>();
            _items.Add(new DummyValues { JobNo = "C-SGN-EXT-17-011-B00", Description = "First", LineType = 1, LineTotal = 1000, PlanningDate = nullDate, PlanningDeliveryDate = nullDate, IsFirstLine = true });
            _items.Add(new DummyValues { JobNo = "C-SGN-EXT-17-011-B00", Description = "Second", LineType = 1, LineTotal = 4500, PlanningDate = nullDate, PlanningDeliveryDate = nullDate, IsFirstLine = false });
            _items.Add(new DummyValues { JobNo = "C-SGN-EXT-17-011-B00", Description = "Thrid", LineType = 1, LineTotal = 7900, PlanningDate = nullDate, PlanningDeliveryDate = nullDate, IsFirstLine = false });

            // WS Payment Schedule Creation
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
             //   throw e;

            }
            return 1;
        }
        //[S-43][T-45][Dayan.P][END]


        static int AttachDimProject(MQMessage message)
        {

         
            DevDimWS.DefDimWS CUDefDim = new DefDimWS();
            CUDefDim.Url = "http://" + message.nav_server + "/WS/" + message.act_legal.Replace(" ", "%20") + "/Codeunit/DefDimWS";
            CUDefDim.Credentials = new NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");                                                //ROLLOUT the new CU and expose it as a webservice in every server.
            CUDefDim.AddDimension(167, "PROJECT", message.project_code, message.project_name, message.contract_code);

            return 1;
        }

       

        static int ProcessCreation(MQMessage message, String Type = "main") {


            // WS Contract Creation
            int BypassReadOnlyAfterCreation = 0;  //used to avoid updating fields that become readonly after creation and first update
            Contracts_Service WSContract = new ContractsWS.Contracts_Service();
            WSContract.Url = "http://"+message.nav_server+"/WS/" + message.act_legal.Replace(" ","%20") + "/Page/Contracts";
            WSContract.Credentials = new NetworkCredential(message.nav_user, message.nav_pass, "archetype-group.com");
            
            message.company_no = message.Navision_Code;
            //If not Map message data to Nav Client Object 
            ContractsWS.Contracts newjob = new ContractsWS.Contracts();

            //apparently Nav manage only local currency, EURO and USD.
            //THEN if currency is not EUR or USD, leave currency blank so it is LCY by default.
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

            //check if client exist

        
           
            newjob.Description = message.contract_name; //contract name
            newjob.Description_2 = message.project_name;
           
           //call creation in NAV
           try { 
            WSContract.Create(ref newjob);

            }
            catch (Exception e)
            {
    
                if (!e.Message.Contains("already exist"))
                {
                    BypassReadOnlyAfterCreation = 1;
                    throw e; // we ignore if project already exist, it will update intranet accordingly to avoid retaking it.
                }
            }

      
            ContractsWS.Contracts jobUpdate = new ContractsWS.Contracts();
            if (Type == "main")
            {
                //TODO : check the case when an addendum is a child of an existing contract...
                //TODO : review the logic... if a contract is an addendum with sub component... need to manage the 3 level.
                
                jobUpdate = WSContract.Read(message.contract_code);
                //check if it is addendum. then if it is the main is always linked to an other contract...
                if(message.contract_type == "Addendum" ) {

                    if (BypassReadOnlyAfterCreation == 0) jobUpdate.Contract_Type = (Contract_Type)Enum.Parse(typeof(Contract_Type), "Addendum");
                    jobUpdate.Primary_Contract_No = message.crm_main_contract_code;
                    jobUpdate.Main_Contract_No = message.contract_code;

                }
                else {
                    if ((BypassReadOnlyAfterCreation == 0 && jobUpdate.Contract_Value == 0)) jobUpdate.Contract_Value = Convert.ToDecimal(message.ContractValue);

                    if (BypassReadOnlyAfterCreation == 0) jobUpdate.Contract_Type = (Contract_Type)Enum.Parse(typeof(Contract_Type), "Primary");
                }
                
            } else
            {
              
                jobUpdate = WSContract.Read(message.ComponentCode);
                //Todo : Check if Add or Compo
                //Todo rename Extentions to component.
                jobUpdate.Contract_Type = (Contract_Type)Enum.Parse(typeof(Contract_Type), "Component");
                jobUpdate.Main_Contract_No = message.contract_code;
               
                //TODO : check if the primary contract exist, if not, empty it and send a mail.
                jobUpdate.Primary_Contract_No = message.contract_code;

                if ( (BypassReadOnlyAfterCreation == 0 && jobUpdate.Contract_Value == 0)) jobUpdate.Contract_Value =  Convert.ToDecimal(message.ComponentValue);

            }
            //TODO : try catch here

            jobUpdate.Legal_entities = (Legal_entities) Enum.Parse(typeof(Legal_entities), message.act_legal_code); // LEGAL Entity ACT signing > Dynamic enum
            jobUpdate.Budget_Type = Budget_Type.DS;  // SCOPE

            //the valued is defined at the begining after checking the customer exist or not. if not its empty
            jobUpdate.Bill_to_Customer_No = message.company_no;
            try
            {
                WSContract.Update(ref jobUpdate);

            }
            catch (Exception e)
            {
                //If Primary, it is readonly...Contract Level must not be Primary in 
                if (!e.Message.Contains("already exist") && !e.Message.Contains("Primary"))   throw e; // we ignore if project already exist, it will update intranet accordingly to avoid retaking it.
            }
           

            return 1;
            
        }
        
    }
}
