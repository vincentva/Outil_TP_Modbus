using ModbusClient;
using System;
using System.IO.Ports;
using System.Linq;

namespace UCBN_EEA_TP_Modbus
{
    class Program
    {
        static void Main(string[] args)
        {
            IModbusClient modbusClient = null;

            ConsoleKeyInfo keyInfo = new ConsoleKeyInfo();
            Console.WriteLine("Voulez vous utiliser une liaison série ou TCP ? s/t");
            do
            {
                try
                {
                    keyInfo = Console.ReadKey();
                    Console.WriteLine();
                    switch (keyInfo.KeyChar)
                    {
                        case 's': modbusClient = CreerMaitreModbusSerie(); break;
                        case 't': modbusClient = CreerClientModbusOnTcp(); break;
                        default: Console.WriteLine("Appuyez sur la touche s ou la touche t"); break;
                    }
                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
                    while (( e = e.InnerException) != null)
                    {
                        Console.Write(" / " + e.Message);
                    }
                    Console.WriteLine();
                    Console.WriteLine("Appuyez sur une touche pour terminer l'application.");
                    Console.ReadKey();
                    return;
                }
            } while ((keyInfo.KeyChar != 's') && (keyInfo.KeyChar != 't'));

            // Boucle principale, s'arrête si une trame vide est entrée
            while (true)
            {
                // Saisie de la requète en hexadécimal (sans CRC ou MBAP)
                Console.Write("Entrez les valeurs hexa : ");
                string chaine = Console.ReadLine();
                if (chaine == "") break;
                Communiquer(modbusClient, chaine);
            }

        }

        static IModbusClient CreerMaitreModbusSerie()
        {
            MaitreModbus maitre = null;
            // Choix du port COM par l'utilisateur et création du maître sur ce port
            int numPort = ChoisirNumPortCom();
            maitre = new MaitreModbus(numPort);
            return maitre;
        }

        static IModbusClient CreerClientModbusOnTcp()
        {
            ClientModbusTcp client = null;
            Console.WriteLine("Entrez l'adresse IP du serveur :");
            string ipServer = Console.ReadLine();
                client = new ClientModbusTcp(ipServer, 502);
                return client;
        }

        private static void Communiquer(IModbusClient modbusClient, string chaine)
        {
            try
            {
                modbusClient.EnvoyerRequete(chaine);
                while (!modbusClient.ReceptionFinie()) { }
            }
            catch (ModbusException e)
            {
                Console.WriteLine("Erreur Modbus : " + e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Cause : " + e.InnerException.Message);
                }
            }
            catch (FormatException e)
            {
                Console.WriteLine("Erreur de formatage d'une chaine de caractère : " + e.Message);
            }
            finally
            {
                // Affichage de la trame envoyée
                Console.WriteLine("Requête : " + modbusClient.LireRequete());
                string reponse = null;
                if ((reponse = modbusClient.LireReponse()) != null)
                {
                    // Affichage de la réponse reçue
                    Console.WriteLine("Réponse : " + reponse);
                }
                else
                {
                    Console.WriteLine("Pas de réponse.");
                }
            }

        }

        static int ChoisirNumPortCom()
        {
            string[] listePorts = SerialPort.GetPortNames();
            Console.Write("Port(s) série trouvé(s) : ");
            foreach (string nomPort in listePorts) { Console.Write(nomPort + ", "); }
            Console.WriteLine();
            Console.WriteLine("Entrez le numéro du port à utiliser : ");
            return int.Parse(Console.ReadLine());
        }

        static string ChoisirPortCom()
        {
            string portChoisi = null;
            string[] listePorts = SerialPort.GetPortNames();
            do
            {
                if (portChoisi != null)
                    Console.WriteLine("Il n'y a pas de port nommé {0}.", portChoisi);
                Console.Write("Port(s) série trouvé(s) : ");
                foreach (string nomPort in listePorts) { Console.Write(nomPort + ", "); }
                Console.WriteLine();
                Console.WriteLine("Entrez le nom du port à utiliser : ");
                portChoisi = Console.ReadLine();
            }
            while (!(listePorts.Contains(portChoisi) || (portChoisi == string.Empty)));
            return portChoisi;
        }

        /// <summary>
        /// Écrit sur la console le contenu d'un tableau d'octets sous forme de caractères hexadécimaux
        /// (deux caractères hexadécimaux pour chaque octet)
        /// </summary>
        /// <param name="nom"></param>
        /// <param name="tabOctets"></param>
        static void ecrireTableauOctets(byte[] tabOctets, string nom)
        {
            if ((tabOctets != null) && (tabOctets.Length > 0))
            {
                Console.Write(nom + " : ");
                foreach (byte octet in tabOctets) Console.Write("{0:x2} ", octet);
                Console.WriteLine();
            }
            else
                if (tabOctets == null)
                Console.WriteLine("La chaine " + nom + " est \"null\".");
            else Console.WriteLine("La chaine " + nom + " est vide.");
        }
    }
}
