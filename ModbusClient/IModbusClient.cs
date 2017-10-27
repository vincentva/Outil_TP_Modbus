using System;
namespace ModbusClient
{
	public interface IModbusClient
	{
		void EnvoyerRequete(string trameHexa);
		bool ReceptionFinie();
		string LireReponse();
		string LireRequete();
	}
}
