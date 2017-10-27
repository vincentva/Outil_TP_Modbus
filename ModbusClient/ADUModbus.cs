using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModbusClient
{
    /// <summary>
    /// Cette classe statique comporte des méthodes auxiliaires pour la création et le décodage de trames (ADU)
    /// Modbus sur ligne série
    /// </summary>
    public static class ADUModbus
    {
        /// <summary>
        /// Prend un entier signé 16 bits et pour créer un tableau de deux octets,
        /// l'octet de poids fort de l'entier est le premier octet du tableau (à l'indice 0).
        /// </summary>
        /// <param name="entier16"></param>
        /// <returns></returns>
        static public byte[] Entier16VersOctetsFortFaible(ushort entier16)
        {
            byte[] tabOctets = new byte[2];
            tabOctets[0] = (byte)entier16;
            tabOctets[1] = (byte)(entier16 >> 8);
            return tabOctets;
        }

        /// <summary>
        /// Prends les deux premiers octets d'un tableaux d'octets pour créer un entier signé 16 bits,
        /// le premier octet du tableau (à l'indice 0) est l'octet de poids fort de l'entier.
        /// </summary>
        /// <param name="tabOctets"></param>
        /// <returns></returns>
        static public ushort OctetsFortFaibleVersEntier16(byte[] tabOctets)
        {
            ushort entier;
            entier = (ushort)tabOctets[0];
            entier += (ushort) (tabOctets[1]*256);
            return entier;
        }

        /// <summary>
        ///  Crée un tableau d'octets à partir d'une chaine de caractères hexadécimaux,
        ///  chaque paire de caractères hexadécimaux est transformée en un octet,
        ///  le premier caractère de la paire génère les 4 bits de poids fort.
        /// </summary>
        /// <param name="chaine"></param>
        /// <returns></returns>
        /// <exception cref="ModbusException">ModbusException lancée si la chaine hexadécimale comporte
        /// un nombre impaire de caractères ou un caractère différent de 0...F</exception>
        static public byte[] ChaineHexaVersOctets(string chaine)
        {
            int nombreOctets;
            try
            {
				int i = 0;
				chaine.Trim();
				while ((i = chaine.IndexOf(' ')) >= 0)
				{
					chaine = chaine.Remove(i, 1);
				}
                if (chaine.Length % 2 == 0) { nombreOctets = chaine.Length / 2; }
                else { throw new ModbusException("Trame invalide : nombre impair de caractères hexadécimaux !"); }
                byte[] tabOctets = new byte[nombreOctets];

                for (i = 0; i < chaine.Length; i += 2)
                {
                    tabOctets[i / 2] = Convert.ToByte(chaine.Substring(i, 2), 16);
                }

                return tabOctets;
            }
            catch (Exception e)
            {
                throw new ModbusException("Erreur lors de la création d'une trame.", e);
            }
        }

        /// <summary>
        /// Crée une chaine de caractères contenant les valeurs hexadécimales des octets contenus dans le tableau
        /// fourni en paramètres 
        /// </summary>
        /// <param name="tabOctets"></param>
        /// <returns></returns>
        static public string OctetsVersChaineHexa(byte[] tabOctets)
        {
            try
            {
                if (tabOctets != null)
                {
                    string chaineRetour = "";
                    foreach (byte octet in tabOctets)
                    {
                        chaineRetour += octet.ToString("x2");
                        chaineRetour += ' ';
                    }
                    return chaineRetour;
                }
                else
                    return null;
            }
            catch (Exception e)
            {
                throw new ModbusException("Erreur lors de la transformation d'un tableau d'octets en chaine hexadécimale", e);
            }
        }

        /// <summary>
        /// Ajout du CRC Modbus à la fin d'un tableau d'octet :
        /// le tableau est agrandi de deux octets où sont stockés la valeur du CRC calculé
        /// </summary>
        /// <param name="trameSansCRC"></param>
        /// <returns></returns>
        static public byte[] ajouterCRC(byte[] trameSansCRC)
        {
            byte[] trameAvecCRC = new byte[trameSansCRC.Length + 2];
            trameSansCRC.CopyTo(trameAvecCRC, 0);
            byte[] crc = Entier16VersOctetsFortFaible(calculCRC(trameSansCRC));
            crc.CopyTo(trameAvecCRC, trameSansCRC.Length);
            return trameAvecCRC;
        }

        /// <summary>
        /// Calcule le CRC d'une trame Modbus fournie sans CRC
        /// </summary>
        /// <param name="trameSansCRC"></param>
        /// <returns></returns>
        static private ushort calculCRC(byte[] trameSansCRC)
        {
            ushort crc = 0xFFFF, lsb = 0x01;
            foreach (byte octet in trameSansCRC)
            {
                crc ^= Convert.ToUInt16(octet);
                for (int i = 0; i < 8; i++)
                {
                    lsb &= crc;
                    crc >>= 1;
                    if (lsb == 0x01) crc ^= 0xA001;
                    lsb = 0x01;
                }
            }
            return crc;
        }

        /// <summary>
        /// Vérifie le CRC présent à la fin d'une trame :
        /// le booléen retourné est vrai si le CRC trouvé à la fin de la trame est identique à celui calculé
        /// </summary>
        /// <param name="trameAvecCRC"></param>
        /// <returns></returns>
        static public bool VerifierCRC(byte[] trameAvecCRC)
        {
            if (trameAvecCRC.Length < 2) return false;

            byte[] reponseSansCRC = new byte[trameAvecCRC.Length - 2];
            byte[] tempCRCRecu = new byte[2];

            Buffer.BlockCopy(trameAvecCRC, trameAvecCRC.Length - 2, tempCRCRecu, 0, 2);
            ushort crcRecu = ADUModbus.OctetsFortFaibleVersEntier16(tempCRCRecu);
            Buffer.BlockCopy(trameAvecCRC, 0, reponseSansCRC, 0, reponseSansCRC.Length);
            ushort crcCalcule = ADUModbus.calculCRC(reponseSansCRC);

            return (crcRecu == crcCalcule);
        }

		static public byte[] ajouterMBAP(byte[] pdu)
		{
			if (pdu.Length > 252)
			{
				throw new ModbusException("Le PDU doit faire moins de 252 octets de long");
			}
			byte[] payLoad = new byte[pdu.Length + 6];
			ushort length = (ushort)pdu.Length;
			payLoad[0] = (byte)(nextTransaction >> 8);
			payLoad[1] = (byte)nextTransaction;
			payLoad[2] = 0;
			payLoad[3] = 0;
			payLoad[4] = (byte) (length >> 8);
			payLoad[5] = (byte) length;
			Buffer.BlockCopy(pdu, 0, payLoad, 6, pdu.Length);
			nextTransaction++;
			return payLoad;
		}

		static ushort nextTransaction = 0;
    }

    public class ModbusException : Exception
    {
        public ModbusException() { }
        public ModbusException(string Message) : base(Message) { }
        public ModbusException(string Message, Exception inner) : base(Message, inner) { }
    }

    public class ModbusTimeoutException : ModbusException
    {
        public ModbusTimeoutException() { }
        public ModbusTimeoutException(string Message) : base(Message) { }
        public ModbusTimeoutException(string Message, Exception inner) : base(Message, inner) { }

    }
}
