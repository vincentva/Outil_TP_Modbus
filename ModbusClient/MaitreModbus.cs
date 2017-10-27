using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;

namespace ModbusClient
{
	public class MaitreModbus : IModbusClient
    {
        /// <summary>
        /// Crée un maître Modbus utilisant le port COM désigné et ouvre le port
        /// </summary>
        /// <param name="numPortCOM"></param>
        /// <param name="bauds"></param>
        /// <param name="parite"></param>
        /// <param name="databits"></param>
        /// <param name="stopbits"></param>
        /// <exception cref="ModbusException"></exception>
        public MaitreModbus(int numPortCOM)
        {
            try
            {
                string nomPortCOM = "COM" + numPortCOM.ToString();
                portCOM = new SerialPort(nomPortCOM, 19200, Parity.Even, 8, StopBits.One);
                portCOM.Open();
            }
            catch (Exception e)
            {
                throw new ModbusException("Erreur lors de la création d'un maître Modbus.", e);
            }
        }

        public MaitreModbus(string nomPortCOM)
        {
            try
            {
                portCOM = new SerialPort(nomPortCOM, 19200, Parity.Even, 8, StopBits.One);
                portCOM.Open();
            }
            catch (Exception e)
            {
                throw new ModbusException("Erreur lors de la création d'un maître Modbus.", e);
            }
        }

        ~MaitreModbus()
        {
            if (portCOM != null)
            {
                portCOM.Close();
            }
        }

        /// <summary>
        /// Envoie une requête Modbus présentée sous la forme d'une chaîne de caractères hexadécimaux
        /// et attend la réponse
        /// </summary>
        /// <param name="trameSansCRC"></param>
        public void EnvoyerRequete(string trameSansCRC)
        {
            try
            {
                portCOM.DiscardInBuffer();
                trameReponse = null;
                finLecture = false;
                attenteEnCours = false;

                // Lancement d'un Thread séparé qui mettra le flag finTimeout à vrai si le délais d'attente de réponse est dépassé
                threadTimeout = new Thread(attenteReponse);
                threadTimeout.Start();

                // On attend que threadTimeout ait effectivement démarré
                while (!attenteEnCours)
                {
                }

                // La méthode lectureReponse() sera appellée lors de la réception de quelque chose sur le port série
                portCOM.DiscardInBuffer();
                portCOM.DataReceived += lectureReponse;

                // Envoi effectif de la requête
                envoyerTrame(trameSansCRC);
            }
            catch (Exception ex)
            {
                throw new ModbusException("Erreur lors de l'envoi d'une requête", ex);
            }

            // On n'a plus qu'à attendre la réception d'une réponse complète ou la fin du Timeout
            while (!finLecture && attenteEnCours)
            {

            }
           
            portCOM.DataReceived -= lectureReponse;

            // attenteEnCours devient faux quand le timeout de réponse est atteint avant de recevoir une réponse
            if (!attenteEnCours)
                throw new ModbusException("Expiration du timer d'attente de réponse !");
            else
            {
                try
                {
                    // Si on est arrivé là, c'est qu'une réponse a été reçue

                    // On arrête le thread qui surveille le timeout de réponse
                    threadTimeout.Abort();
                    threadTimeout.Join();
                }
                catch (Exception ex)
                {
                    throw new ModbusException("Erreur lors de l'arrêt du thread de timeout", ex);
                }

                // On vérifie la réponse reçue qui a été stockée dans trameReponse par lectureReponse()
                if (trameReponse == null)
                    throw new ModbusException("Lecture finie avant le timeout mais la réponse est vide.");
                else if (!ADUModbus.VerifierCRC(trameReponse))
                    throw new ModbusException("Réponse incomplète ou corrompue (erreur de CRC).");
            }
        }

		public bool ReceptionFinie()
		{
			return finLecture;
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
            trameRequete = ADUModbus.ajouterCRC(ADUModbus.ChaineHexaVersOctets(chaineHexadecimale));
        }

        private void envoyerTrame(string trameSansCRC)
        {
            creerTrameRequete(trameSansCRC);
            portCOM.Write(trameRequete, 0, trameRequete.Length);
        }


        private void lectureReponse(object sender, EventArgs e)
        {
            int nbOctetsALire = 0;
            int nbOctetsLus = 0;
            int indexFinReponse = 0;
            try
            {
                while ((nbOctetsALire = portCOM.BytesToRead) > 0)
                {
                    if (indexFinReponse + nbOctetsALire > 255)
                        nbOctetsALire = 256 - indexFinReponse;
                    nbOctetsLus = portCOM.Read(tamponReponse, indexFinReponse, nbOctetsALire);
                    indexFinReponse += nbOctetsLus;
                    Thread.Sleep(50);   // On laisse un peu de temps pour que la suite de la trame arrive
                }
                trameReponse = new byte[indexFinReponse];
                Buffer.BlockCopy(tamponReponse, 0, trameReponse, 0, indexFinReponse);
                finLecture = true;
            }
            catch (Exception ex)
            {
                throw new ModbusException("Problème de réception", ex);
            }
        }

        private void attenteReponse()
        {
            attenteEnCours = true;
            Thread.Sleep(dureeTimeout);
            attenteEnCours = false;
        }

        private SerialPort portCOM;
        public byte[] TrameRequete { get { return trameRequete; } }
        private byte[] trameRequete;
        public byte[] TrameReponse { get { return trameReponse; } }
        private byte[] trameReponse;
        private byte[] tamponReponse = new byte[256];
        Thread threadTimeout;
        private const int dureeTimeout = 1000;
        volatile private bool attenteEnCours = false;
        volatile private bool finLecture = false;
    }
}
