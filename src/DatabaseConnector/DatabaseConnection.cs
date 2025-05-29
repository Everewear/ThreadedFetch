using MySql.Data;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Threading.Tasks;

public class DatabaseConnector
{
    private static MySqlCommand myCommand = new();
    private static MySqlConnectionStringBuilder sb = new()
        {
            Server = "localhost",
            Port = 3306,
            UserID = "root",
            Password = "12385",
            Database = "test"
        };
    private static MySqlConnection myConnection = new MySqlConnection(sb.ConnectionString);

    public static void Database() {

        try {
            myConnection.Open();
            DataTable table = myConnection.GetSchema("Tables");
            DisplayData(table);
            CreateTable();
            UpdateTable();
        }
        catch (MySqlException ex) {
            switch (ex.Number) {
                case 0:
                Console.WriteLine("Cannot connect to server. Contact administrator");
                break;
                case 1045:
                Console.WriteLine("Invalid username/password, please try again");
                break;
            }
        }
        catch (Exception e) {
            Console.WriteLine(e);
        }
        finally {
            myConnection.Close();
        }
    }

    private static void DisplayData(DataTable table) {
        foreach (DataRow row in table.Rows) {
            foreach (DataColumn col in table.Columns) {
                Console.WriteLine("{0} {1}", col.ColumnName, row[col]);
            }
            Console.WriteLine("==============================");
        }
    }

    private static void CreateTable() {
        // Based on documentation for dotnet mysql connector
        try{
            myCommand.CommandTimeout = 100;
            myCommand.Connection = myConnection;
            myCommand.CommandText = "DROP PROCEDURE IF EXISTS add_emp";
            myCommand.ExecuteNonQuery();
            myCommand.CommandText = "DROP TABLE IF EXISTS emp";
            myCommand.ExecuteNonQuery();
            myCommand.CommandText = "CREATE TABLE emp (" +
            "empno INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "first_name VARCHAR(20), last_name VARCHAR(20), birthdate DATE)";
            myCommand.ExecuteNonQuery();

        } catch(MySqlException ex){
            Console.WriteLine(ex);
        }
    }

    private static void UpdateTable(){
        try {
            myCommand.CommandTimeout = 60;
            myCommand.CommandText = "INSERT INTO emp VALUES(NULL, @text1, @text, @date)";

            myCommand.Parameters.Clear();
            
            myCommand.Parameters.AddWithValue("@date", DateTime.Now);
            myCommand.Parameters.AddWithValue("@text", "One");
            myCommand.Parameters.AddWithValue("@text1", "Two");

            myCommand.Prepare();

            for (int i = 1; i <= 10; i++) {
                myCommand.Parameters["@date"].Value = DateTime.Now;
                myCommand.Parameters["@text1"].Value = i;
                myCommand.Parameters["@text"].Value = "A string value";

                myCommand.ExecuteNonQuery();
            }
        }
        catch (MySqlException ex) {
            Console.WriteLine(ex);
        }
    }

}