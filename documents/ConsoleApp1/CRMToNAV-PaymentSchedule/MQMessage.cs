using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using EasyNetQ;
namespace ConsoleApp1
{


   

    [Queue("ContractCreationQueue", ExchangeName = "ContractCreationRequest")]
    public class MQMessage
    {
        public string Text { get; set; }
        public string company_no { get; set; }
        public string company_name { get; set; }
        public string legal_name { get; set; }
        public string registration_no { get; set; }
        public string tax_no { get; set; }
        public string address { get; set; }
        public string act_legal { get; set; }

        public string act_legal_code { get; set; }
        public string country_code { get; set; }
        public string nav_server { get; set; }
        public string nav_user { get; set; }
        public string nav_pass { get; set; }
        public string contract_title { get; set; }
        public string contract_code { get; set; }
        public string contract_id { get; set; }
        
        public string client_financial_id { get; set; }
        public string returned_nav_id { get; set; }
        public string VAT_posting { get; set; }
        public string CountryCallingCode { get; set; }
        public string sector_id { get; set; }
        public string Navision_Code { get; set; }
        //For Contract


        public string contract_name { get; set; }

        public int project_id { get; set; }
        public string contract_type { get; set; }
        public string crm_main_contract_code { get; set; }
        //contact info
        public string manager_in_charge { get; set; } //email
        public string manager_code { get; set; }
        public string manager_name { get; set; }
        //contact info
        public string managing_director { get; set; } //email
        public string managing_director_name { get; set; }
        public string fm_mail { get; set; }
        public string fm_name { get; set; }
        public string fc_mail { get; set; }
        public string fc_name { get; set; }

        public string validator_mail { get; set; }

        public string validator_name { get; set; }
        public string bdd_mail { get; set; }
        public string bdd_name { get; set; }

        public int office_id { get; set; }
        public string project_name { get; set; }
        public string VATRate { get; set; }
        public string WHTRate { get; set; }
        public string isWHT_applicable { get; set; }
        public string PaymentTerm { get; set; }
        public double ContractValue { get; set; }
        public string project_code { get; set; }
        public string ComponentName { get; set; }
        public int ComponentId { get; set; }
        public string ComponentCode { get; set; }
        public string ComponentValue { get; set; }
        public string ComponentCurrency { get; set; }
        public string CountryIso3c { get; set; }
    }
}
