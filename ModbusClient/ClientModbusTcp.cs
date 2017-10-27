using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Timers;

namespace ModbusClient
{
    public class ClientModbusTcp : IModbusClient
    {
        /// <summary>
        /// Crée un maître Modbus utilisant le port TCP désigné et ouvre le port
        /// </summary>
        /// <param name="serverPort"></param>
        /// <exception cref="ModbusException"></exception>
        public ClientModbusTcp(string serverIp, int serverPort)
        {
            try
            {
                socket = new TcpClient(serverIp, serverPort);   // Create TcpClient object and connects to server
            }
            catch (IOException ioe)
            {
                throw HandleIOException(ioe);
            }
            catch (SocketException soe)
            {
                if (soe.ErrorCode == 10060)
                {
                    throw new ModbusTimeoutException("Echec de la connexion : pas de reponse du serveur", soe);
                }
                else
                {
                    throw new ModbusException("Erreur de socket lors de la création du client Modbus, code " + soe.ErrorCode.ToString(), soe);
                }
            }
            catch (Exception e)
            {
                throw new ModbusException("Erreur lors de la création du client Modbus.", e);
            }
            socket.ReceiveTimeout = dureeTimeout;
        }

        ~ClientModbusTcp()
        {
            if (socket != null)
            {
                socket.Close();
            }
        }

        /// <summary>
        /// Envoie une requête Modbus présentée sous la forme d'une chaîne de caractères hexadécimaux
        /// et attend la réponse
        /// </summary>
        /// <param name="trameSansMBAP"></param>
        public void EnvoyerRequete(string trameSansMBAP)
        {
            try
            {
                trameReponse = null;
                finReception = false;

                threadRead = new Thread(recevoirReponse);
                threadRead.Start();

                // Envoi  de la requête
                envoyerTrame(trameSansMBAP);
            }
            catch (Exception ex)
            {
                throw new ModbusException("Erreur lors de l'envoi d'une requête", ex);
            }
        }

        public bool ReceptionFinie()
        {
            return finReception;
        }

        public string LireReponse()
        {
            return ADUModbus.OctetsVersChaineHexa(TrameReponse);
        }

        public string LireRequete()
        {
            return ADUModbus.OctetsVersChaineHexa(TrameRequete);
        }

        private void creerTrameRequete(string chaineHexadecimale)
        {
            trameRequete = ADUModbus.ajouterMBAP(ADUModbus.ChaineHexaVersOctets(chaineHexadecimale));
        }

        private void envoyerTrame(string trameSansMBAP)
        {
            creerTrameRequete(trameSansMBAP);
            socket.GetStream().Write(trameRequete, 0, trameRequete.Length);
        }


        private void recevoirReponse()
        {
            NetworkStream readNws = socket.GetStream();
            int nbOctetsALire = 0;
            int nbOctetsLus = 0;
            int indexFinReponse = 0;

            try
            {
                nbOctetsLus = readNws.Read(tamponReponse, 0, 7);
                if (nbOctetsLus > 0)
                {
                    nbOctetsALire = tamponReponse[4] * 256 + tamponReponse[5] - 1;
                    while ((indexFinReponse < nbOctetsALire) || (nbOctetsLus == 0))
                    {
                        nbOctetsLus = readNws.Read(tamponReponse, indexFinReponse + 7, nbOctetsALire - indexFinReponse);
                        Console.WriteLine("{0} octets lus.", nbOctetsLus);
                        indexFinReponse += nbOctetsLus;
                        Thread.Sleep(50);   // On laisse un peu de temps pour que la suite de la trame arrive
                    }
                    trameReponse = new byte[indexFinReponse + 7];
                    Buffer.BlockCopy(tamponReponse, 0, trameReponse, 0, indexFinReponse + 7);
                }
            }
            catch (IOException ioe)
            {

                ModbusException moe = HandleIOException(ioe);
                if (!(moe is ModbusTimeoutException))
                {
                    throw moe;
                }
            }
            finally
            {
                finReception = true;
            }
        }

        private ModbusException HandleIOException(IOException ioe)
        {
            ModbusException moe;
            if (ioe.InnerException is SocketException)
            {
                SocketException soe = ioe.InnerException as SocketException;
                //attenteEnCours = false;
                if (soe.ErrorCode == 10060) // Read timed out
                {
                    moe = new ModbusTimeoutException("Expiration du timer d'attente de connexion ou de réponse.", soe);
                }
                else
                {
                    moe = new ModbusException("Erreur de connexion ou de lecture du Socket.", soe);
                }
            }
            else
            {
                moe = new ModbusException("IOException innattendue.", ioe);
            }
            return moe;
        }


        private TcpClient socket;
        public byte[] TrameRequete { get { return trameRequete; } }
        private byte[] trameRequete;
        public byte[] TrameReponse { get { return trameReponse; } }
        private byte[] trameReponse;
        private byte[] tamponReponse = new byte[258];
        Thread threadRead;
        private const int dureeTimeout = 1000;  // en millisecondes
        volatile private bool finReception = false;
        //private bool reponseRecue;
    }
}
