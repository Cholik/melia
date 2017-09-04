﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Melia.Login.World;
using Melia.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melia.Login.Database
{
	/// <summary>
	/// A player's account.
	/// </summary>
	public class Account
	{
		private object _moneyLock = new object();
		private List<Character> _characters;

		/// <summary>
		/// Account id.
		/// </summary>
		public long Id { get; set; }

		/// <summary>
		/// Account name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Account password.
		/// </summary>
		public string Password { get; set; }

		/// <summary>
		/// Gets or sets account's team name, also updates all characters.
		/// </summary>
		public string TeamName
		{
			get { return _teamName; }
			set
			{
				_teamName = value;
				lock (_characters) _characters.ForEach(a => a.TeamName = value);
			}
		}
		private string _teamName;

		/// <summary>
		/// Amount of medals (iCoins).
		/// </summary>
		public int Medals { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public int GiftMedals { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public int PremiumMedals { get; set; }

		/// <summary>
		/// Id of the barrack map.
		/// </summary>
		public int SelectedBarrack { get; set; }

		/// <summary>
		/// The layer that is currently selected as being viewed.
		/// </summary>
		public int SelectedBarracklayer { get; set; }

		/// <summary>
		/// Creates new account.
		/// </summary>
		public Account()
		{
			_characters = new List<Character>();

			this.Medals = 500;
			this.SelectedBarrack = 11;
		}

		/// <summary>
		/// Returns list of all characters on account.
		/// </summary>
		/// <returns></returns>
		public Character[] GetCharacters()
		{
			lock (_characters)
				return _characters.ToArray();
		}

		/// <summary>
		/// Returns character by index, or null if it doesn't exist.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public Character GetCharacterByIndex(byte index)
		{
			lock (_characters)
				return _characters.FirstOrDefault(a => a.Index == index);
		}

		/// <summary>
		/// Returns character by character id, or null if it doesn't exist.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public Character GetCharacterById(long id)
		{
			lock (_characters)
				return _characters.FirstOrDefault(a => a.Id == id);
		}

		/// <summary>
		/// Adds character to account object and assigns index and team name.
		/// </summary>
		/// <param name="character"></param>
		private void AddCharacter(Character character)
		{
			lock (_characters)
			{
				for (byte i = 1; i <= byte.MaxValue; ++i)
				{
					if (!_characters.Any(a => a.Index == i))
					{
						character.Index = i;
						break;
					}
				}

				character.TeamName = this.TeamName;

				_characters.Add(character);
			}
		}

		/// <summary>
		/// Removes character from account object.
		/// </summary>
		/// <param name="character"></param>
		private void RemoveCharacter(Character character)
		{
			lock (_characters)
				_characters.Remove(character);
		}

		/// <summary>
		/// Loads account with given name from database, incl. characters,
		/// and returns it.
		/// </summary>
		/// <param name="accountName"></param>
		/// <returns></returns>
		public static Account LoadFromDb(string accountName)
		{
			var account = LoginServer.Instance.Database.GetAccount(accountName);
			if (account == null)
				return null;

			var characters = LoginServer.Instance.Database.GetCharacters(account.Id);
			foreach (var character in characters)
			{
				if (character.MapId == 0)
					character.MapId = 1001;

				account.AddCharacter(character);
			}

			return account;
		}

		/// <summary>
		/// Removes character from account and deletes it from the database.
		/// </summary>
		/// <param name="character"></param>
		/// <returns></returns>
		public bool DeleteCharacter(Character character)
		{
			if (!_characters.Contains(character))
				return false;

			// If the deletion on the db fails, the character shouldn't
			// have been shown to begin with and should be removed.
			// If it doesn't fail, the removal is valid as well,
			// do this regardless of the query result.
			this.RemoveCharacter(character);

			return LoginServer.Instance.Database.DeleteCharacter(character.Id);
		}

		/// <summary>
		/// Adds character to account and the database.
		/// </summary>
		/// <param name="character"></param>
		public void CreateCharacter(Character character)
		{
			LoginServer.Instance.Database.CreateCharacter(this.Id, character);
			this.AddCharacter(character);
		}

		/// <summary>
		/// Sets the selected barrack layer.
		/// </summary>
		/// <param name="layer"></param>
		public void SetSelectedBarrackLayer(int layer)
		{
			this.SelectedBarracklayer = layer;
		}

		/// <summary>
		/// Used to check if the account has enough combined medals to afford a purchase.
		/// </summary>
		/// <param name="cost"></param>
		/// <returns></returns>
		public bool CanAffordPurchase(int cost)
		{
			lock (_moneyLock)
				return cost <= this.Medals + this.GiftMedals + this.PremiumMedals;
		}

		/// <summary>
		/// Processes a charge attempt on the account.
		/// </summary>
		/// <param name="cost"></param>
		/// <returns>Returns 'true' on a successful charge.</returns>
		public bool Charge(int cost)
		{
			lock (_moneyLock)
			{
				if (!this.CanAffordPurchase(cost))
					return false;

				var oldMedals = this.Medals;
				var oldGiftMedals = this.GiftMedals;
				var oldPremiumMedals = this.PremiumMedals;

				this.Medals -= cost;
				if (this.Medals >= 0)
					return true;

				this.GiftMedals += this.Medals;
				this.Medals = 0;
				if (this.GiftMedals >= 0)
					return true;

				this.PremiumMedals += this.GiftMedals;
				this.GiftMedals = 0;
				if (this.PremiumMedals >= 0)
					return true;

				// Revert the transaction.
				this.Medals = oldMedals;
				this.GiftMedals = oldGiftMedals;
				this.PremiumMedals = oldPremiumMedals;

				return false;
			}
		}

		/// <summary>
		/// Saves account and characters in database.
		/// </summary>
		public void Save()
		{
			LoginServer.Instance.Database.SaveAccount(this);
			foreach (var character in _characters)
				LoginServer.Instance.Database.SaveCharacter(character);
		}
	}
}
