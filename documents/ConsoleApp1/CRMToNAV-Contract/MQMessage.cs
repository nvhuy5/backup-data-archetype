using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using EasyNetQ;
using System.Data.SqlClient;
using System.Reflection;
using System.Text.RegularExpressions;

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

        public string custom_act_legal_code { get; set; }
        public string country_code { get; set; }
        public string nav_server { get; set; }
        public string nav_user { get; set; }
        public string nav_pass { get; set; }
        public string contract_title { get; set; }
        public string contract_code { get; set; }
        public string contract_id { get; set; }

        public string next_status { get; set; }

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
      
        public string manager_in_charge { get; set; } 
        public string manager_code { get; set; }
        public string manager_name { get; set; }
      
        public string managing_director { get; set; } 
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
        public double VATRate { get; set; }
        public double WHTRate { get; set; }
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


        public string GetNextStatus(string next_status)
        {
            if (string.IsNullOrWhiteSpace(next_status) || !next_status.Contains('.'))
                return next_status; 

            try
            {
          
                int lastDotIndex = -1, quotes = 0;
                for (int i = 0; i < next_status.Length && next_status[i] != '('; i++)
                    if (next_status[i] == '"' || next_status[i] == '\'') quotes ^= 1;
                    else if (next_status[i] == '.' && quotes == 0) lastDotIndex = i;
                string typePart = next_status.Substring(0, lastDotIndex);
                var rest = next_status.Substring(lastDotIndex + 1);

                var parenIndex = rest.IndexOf('(');
                var methodPart = parenIndex >= 0 ? rest.Substring(0, parenIndex) : rest;
                var argsPart = parenIndex >= 0
                    ? rest.Substring(parenIndex + 1).TrimEnd(')')
                    : "";


                Type type = Type.GetType(typePart);
                var method = type.GetMethod(methodPart,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
           
                object[] args = string.IsNullOrWhiteSpace(argsPart)
                    ? new object[0]
                    : argsPart.Split(',')
                              .Select(a => a.Trim().Trim('"', '\''))
                              .Select(a =>
                              {
                                  int i; double d; bool b;
                                  if (int.TryParse(a, out i)) return (object)i;
                                  if (double.TryParse(a, out d)) return (object)d;
                                  if (bool.TryParse(a, out b)) return (object)b;
                                  return (object)a;
                              })
                              .ToArray();

                object instance = method.IsStatic ? null : Activator.CreateInstance(type);
                var result = method.Invoke(instance, args);
                return result != null ? next_status.ToString() : null;
            }
            catch (Exception e)
            {
                return "Error getting next steps";
            }
        }
    

    public async Task<bool> GetNextStep(string connectionString, string query)
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
    }

}


