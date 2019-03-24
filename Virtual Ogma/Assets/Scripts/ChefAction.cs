﻿using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Linq;

public class ChefAction : MonoBehaviour
{
	[SerializeField] GameObject character;
	bool is_listening = false;

	KeywordsData keywords_data;
	Inventory inventory;

	List<string> keywords_list = new List<string>();
	Dictionary<Transform, Vector3> place_position = new Dictionary<Transform, Vector3>();
	Dictionary<Item, Vector3> item_positions = new Dictionary<Item, Vector3>();

	KeywordRecognizer keyword_recognizer;
	Dictionary<string, Action> keywords_dict = new Dictionary<string, Action>();

	[SerializeField] private GameObject countdown_display_prefab;
	[SerializeField] private Vector3 countdown_display_position_offset;

	public void init_cooking(GameObject ch)
	{
		if (Input.GetKey(KeyCode.Q))
		{
			character = ch;
			ch.GetComponent<Player>().highlight_player();
			place_position = ch.GetComponent<ChefData>().chef_interactable_positions;
			// pan the camera towards the selected character and follow the player until 'Q' is released
			is_listening = true;
		}
	}

	void Start()
	{
		inventory = Inventory.instance;             // get the Singleton instance of Inventory Class
		keywords_data = KeywordsData.instance;      // get the Singleton instance of KeywordsData Class
		
		keywords_list = keywords_data.chef_keywords_2;
		item_positions = keywords_data.game_item_positions;

		foreach (string keyword in keywords_list)
		{
			string[] word_list = keyword.Split();
			keywords_dict.Add(keyword, () => actionSelection(word_list));
		}
		// THERE IS SOME PROBLEM HERE! I used to get null reference exception until I Debug.Log'ed the contents of the dictionary, after which the errors simply disappeared.. NANI?

		keyword_recognizer = new KeywordRecognizer(keywords_dict.Keys.ToArray(), ConfidenceLevel.Low);
		keyword_recognizer.OnPhraseRecognized += OnKeywordsRecognized;
	}

	public void OnKeywordsRecognized(PhraseRecognizedEventArgs args)
	{
		Action keyword_action;
		if (keywords_dict.TryGetValue(args.text, out keyword_action))
		{
			Test_script2.ts2.applyText(args.text);
			keyword_action.Invoke();
		}
	}

	void Update()
	{
		if (Input.GetKey(KeyCode.Q))
		{
			if (!keyword_recognizer.IsRunning && is_listening)
			{
				Debug.Log("give a cooking command");
				Test_script2.ts2.applyText("give a cooking command");
				keyword_recognizer.Start();
			}
		}
		else
		{
			if (keyword_recognizer.IsRunning && is_listening)               // (keyword_recognizer.IsRunning) could be replaced with (is_listening)
			{
				is_listening = false;
				keyword_recognizer.Stop();
				character.GetComponent<Player>().remove_highlighter();
				// pan the camera to the default camera position
			}
		}
	}


	void actionSelection(string[] word_list)
	{
		switch (word_list[0])
		{
			case "chop":
				StartCoroutine(chopping(word_list));
				break;
			case "boil":
				StartCoroutine(boiling(word_list));
				break;
			case "turn":
				StartCoroutine(turn_off(word_list));
				break;
			case "get":
				StartCoroutine(getting_supplies(word_list));
				break;
			case "wash":
				StartCoroutine(washing(word_list));
				break;
			case "prepare":
				StartCoroutine(preparing(word_list));
				break;
			default:
				default_method();
				break;
		}
	}

	IEnumerator chopping(string[] word_list)
	{
		GameObject player_GO = character;          // A copy of character is made for use within the coroutine since, the global variable character might change before this coroutine ends.
		string cutting_board_name = word_list[word_list.Length - 1];
		GameObject cutting_board_GO = GameObject.Find("/Props/cutting_boards/" + cutting_board_name.ToLower());                  // !!!!!! Find GameObject with name !!!!!!
		if (!player_GO.GetComponent<Player>().is_busy && !cutting_board_GO.GetComponent<CuttingBoard>().is_chopping)
		{
			string item_name = word_list[1];                     // received item to be chopped vegetables/fruits/meat
			Item food_item = keywords_data.findItemWithRawMaterial("chopped " + item_name);
			Vector3[] positions = { item_positions[food_item], place_position[cutting_board_GO.transform], player_GO.GetComponent<Player>().starting_position };        // array of positions where character needs to go

			Player ch = player_GO.GetComponent<Player>();
			CuttingBoard cb = cutting_board_GO.GetComponent<CuttingBoard>();
			ch.is_busy = true;          // set character.is_busy true
			cb.is_chopping = true;      // set cutting_board.is_chopping true

			//move player to fetch item
			Debug.Log("fetch " + item_name + " for chopping");
			ch.target = positions[0];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;

			//play get_supplies sound
			AudioManager.instance.playSound("get_supplies");
			
			//wait player for 1s
			yield return new WaitForSeconds(1);

			//move player to chopping board
			Debug.Log("move to chopping board");
			ch.target = positions[1];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;

			//resolve rotations
			Vector3 direction = cb.transform.position - ch.transform.position;
			ch.resolveRotationAfterNavigation(direction);

			//call cb.chop()
			cb.chop(food_item);

			//wait player for chopping_delay seconds
			while (cb.is_chopping)
				yield return null;
			
			//stop resolve_rotations_coroutine
			if (ch.resolve_rotations_coroutine != null)
				StopCoroutine(ch.resolve_rotations_coroutine);

			//move player to starting position
			ch.target = positions[2];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;
			Debug.Log("player returns");

			//resolve rotations
			direction = ch.starting_rotation;
			Coroutine resolve_rotations_on_return = StartCoroutine(ch.resolveRotations(direction));
			yield return new WaitForSeconds(1);
			//stop resolve_rotations_on_return
			if (resolve_rotations_on_return != null)
				StopCoroutine(resolve_rotations_on_return);

			//set chef not busy
			ch.is_busy = false;
		}
	}

	IEnumerator boiling(string[] word_list)
	{
		GameObject player_GO = character;          // A copy of character is made for use within the coroutine since, the global variable character might change before this coroutine ends.
		string cooker_name = word_list[word_list.Length - 1];
		GameObject cooker_GO = GameObject.Find("/Props/cookers/" + cooker_name.ToLower());                  // !!!!!! Find GameObject with name !!!!!!
		if (!player_GO.GetComponent<Player>().is_busy && !cooker_GO.GetComponent<Cooker>().is_cooking)      // if character is free and cooker is not cooking anything
		{
			string item_name = word_list[1];                     //received item to be cooked rice/noodles
			Item food_item = keywords_data.findItemWithRawMaterial("boiled " + item_name);
			Vector3[] positions = { item_positions[food_item], place_position[cooker_GO.transform], player_GO.GetComponent<Player>().starting_position };        //array of positions where character needs to go

			Player ch = player_GO.GetComponent<Player>();
			Cooker co = cooker_GO.GetComponent<Cooker>();
			ch.is_busy = true;          // set character.is_busy true
			co.is_cooking = true;       // set cooker.is_cooking true

			//move player to fetch item
			Debug.Log("fetch " + item_name + " for boiling");
			ch.target = positions[0];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;

			//play get_supplies sound
			AudioManager.instance.playSound("get_supplies");

			//wait player for 1s
			yield return new WaitForSeconds(1);

			//move player to cooker
			Debug.Log("move to cooker");
			ch.target = positions[1];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;

			//resolve rotations
			Vector3 direction = co.transform.position - ch.transform.position;
			ch.resolveRotationAfterNavigation(direction);

			//call co.cook()
			Debug.Log("reached cooker");
			co.cook(food_item);

			//DO NOT wait player for cooking_delay seconds. Instead, wait player at cooker for 1s
			yield return new WaitForSeconds(1);
			
			//stop resolve_rotations_coroutine
			if (ch.resolve_rotations_coroutine != null)
				StopCoroutine(ch.resolve_rotations_coroutine);

			//move player to starting position
			ch.target = positions[2];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;
			Debug.Log("player returns");

			//resolve rotations
			direction = ch.starting_rotation;
			Coroutine resolve_rotations_on_return = StartCoroutine(ch.resolveRotations(direction));
			yield return new WaitForSeconds(1);
			//stop resolve_rotations_on_return
			if (resolve_rotations_on_return != null)
				StopCoroutine(resolve_rotations_on_return);

			//set chef not busy
			ch.is_busy = false;
		}
	}

	IEnumerator turn_off(string[] word_list)
	{
		GameObject player_GO = character;          // A copy of character is made for use within the coroutine since, the global variable character might change before this coroutine ends.
		string cooker_name = word_list[word_list.Length - 1];
		GameObject cooker_GO = GameObject.Find("/Props/cookers/" + cooker_name.ToLower());                  // !!!!!! Find GameObject with name !!!!!!
		if (!player_GO.GetComponent<Player>().is_busy && cooker_GO.GetComponent<Cooker>().is_cooking)       // if character is free and cooker is cooking something
		{
			Vector3[] positions = { place_position[cooker_GO.transform], player_GO.GetComponent<Player>().starting_position };        //array of positions where character needs to go

			Player ch = player_GO.GetComponent<Player>();
			Cooker co = cooker_GO.GetComponent<Cooker>();
			ch.is_busy = true;          // set character.is_busy true, and cooker.is_cooking is already true

			//move player to cooker
			Debug.Log("move to cooker");
			ch.target = positions[0];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;

			//resolve rotations
			Vector3 direction = co.transform.position - ch.transform.position;
			ch.resolveRotationAfterNavigation(direction);

			//call co.turn_off_cooker()
			Debug.Log("reached cooker");
			co.turn_off_cooker();

			yield return new WaitForSeconds(.5f);       // player takes 0.5s to turn off cooker

			//stop resolve_rotations_coroutine
			if (ch.resolve_rotations_coroutine != null)
				StopCoroutine(ch.resolve_rotations_coroutine);

			//move player to starting position
			ch.target = positions[1];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;
			Debug.Log("player returns");

			//resolve rotations
			direction = ch.starting_rotation;
			Coroutine resolve_rotations_on_return = StartCoroutine(ch.resolveRotations(direction));
			yield return new WaitForSeconds(1);
			//stop resolve_rotations_on_return
			if (resolve_rotations_on_return != null)
				StopCoroutine(resolve_rotations_on_return);

			//set chef not busy
			ch.is_busy = false;
		}
	}

	IEnumerator getting_supplies(string[] word_list)
	{
		GameObject player_GO = character;          // A copy of character is made for use within the coroutine since, the global variable character might change before this coroutine ends.
		if (!player_GO.GetComponent<Player>().is_busy)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 1; i < word_list.Length; i++)
			{
				sb.Append(" " + word_list[i]);          // for items such as "sea weed", item name consist of 2 words. Therefore, we have to use StringBuilder to set item name of any size
			}
			string item_name = sb.ToString().Trim();         // item to be get buns/sea weed
			Item food_item = keywords_data.findItemWithRawMaterial(item_name);
			Vector3[] positions = { item_positions[food_item], player_GO.GetComponent<Player>().starting_position };        // array of positions where character needs to go

			Player ch = player_GO.GetComponent<Player>();
			ch.is_busy = true;          // set character.is_busy true

			//move player to fetch item
			Debug.Log("get item from " + item_positions[food_item]);
			ch.target = positions[0];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;

			//play get_supplies sound
			AudioManager.instance.playSound("get_supplies");

			//wait player for 1s
			yield return new WaitForSeconds(food_item.time_to_prepare);

			//add item to inventory... DONE
			bool has_added = inventory.addItem(food_item);
			if (has_added) {
				Debug.Log(food_item.name + " added to inventory");
			}
			else {
				Debug.Log("can not add " + food_item.name + " to inventory");
				Test_script2.ts2.applyText("can not add " + food_item.name + " to inventory");
			}
			//move player to starting position
			ch.target = positions[1];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;
			Debug.Log("player returns");

			//resolve rotations
			Vector3 direction = ch.starting_rotation;
			Coroutine resolve_rotations_on_return = StartCoroutine(ch.resolveRotations(direction));
			yield return new WaitForSeconds(1);
			//stop resolve_rotations_on_return
			if (resolve_rotations_on_return != null)
				StopCoroutine(resolve_rotations_on_return);

			//set chef not busy
			ch.is_busy = false;
		}
	}

	IEnumerator washing(string[] word_list)
	{
		GameObject player_GO = character;          // A copy of character is made for use within the coroutine since, the global variable character might change before this coroutine ends.
		GameObject sink_GO = GameObject.Find("/Props/sink");                  // !!!!!! Find GameObject with name !!!!!!
		GameObject serving_table_GO = GameObject.Find("/Props/serving_table");
		if (!player_GO.GetComponent<Player>().is_busy && !sink_GO.GetComponent<Sink>().is_washing)
		{
			string item_name = word_list[1];                     // received item to be washed (dishes)
			Item utensil_item = keywords_data.findItemWithRawMaterial(item_name);
			Vector3[] positions = { place_position[serving_table_GO.transform], place_position[sink_GO.transform], player_GO.GetComponent<Player>().starting_position };        // array of positions where character needs to go

			Player ch = player_GO.GetComponent<Player>();
			Sink sink = sink_GO.GetComponent<Sink>();
			ch.is_busy = true;              // set character.is_busy true
			sink.is_washing = true;         // set sink.is_washing true

			//move player to fetch item
			Debug.Log("fetch " + utensil_item.name);
			ch.target = positions[0];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;

			//wait player fetching item
			yield return new WaitForSeconds(.5f);

			//move player to sink
			Debug.Log("move to sink");
			ch.target = positions[1];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;

			//resolve rotations
			Vector3 direction = sink.transform.position - ch.transform.position;
			ch.resolveRotationAfterNavigation(direction);

			//call sink.wash_plates()
			Debug.Log("reached sink");
			sink.washUtensils(utensil_item);

			//wait player for washing_time seconds
			while (sink.is_washing)
				yield return null;

			//stop resolve_rotations_coroutine
			if (ch.resolve_rotations_coroutine != null)
				StopCoroutine(ch.resolve_rotations_coroutine);

			//move player to starting position
			ch.target = positions[2];
			ch.target_reached = false;
			while (!ch.target_reached)
				yield return null;
			Debug.Log("player returns");

			//resolve rotations
			direction = ch.starting_rotation;
			Coroutine resolve_rotations_on_return = StartCoroutine(ch.resolveRotations(direction));
			yield return new WaitForSeconds(1);
			//stop resolve_rotations_on_return
			if (resolve_rotations_on_return != null)
				StopCoroutine(resolve_rotations_on_return);

			//set chef not busy
			ch.is_busy = false;
		}
	}

	IEnumerator preparing(string[] word_list)
	{
		GameObject player_GO = character;          // A copy of character is made for use within the coroutine since, the global variable character might change before this coroutine ends.
		if (!player_GO.GetComponent<Player>().is_busy)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 1; i < word_list.Length; i++)
			{
				sb.Append(" " + word_list[i]);          // for dishes such as "french fries", dish name consist of 2 words. Therefore, we have to use StringBuilder to set dish name of any size
			}
			string dish_name = sb.ToString().Trim();         // dish to prepare eg.- salad,biryani,sushi,etc..
			Item dish_item = keywords_data.findItemWithRawMaterial(dish_name);
			
			bool are_ingredients_available = inventory.areIngredientsAvailable(dish_item);  // check if raw materials for dish are available in inventory
			if (are_ingredients_available)
			{
				Vector3[] positions = { player_GO.GetComponent<Player>().preparing_position.position, player_GO.GetComponent<Player>().starting_position };
				Player ch = player_GO.GetComponent<Player>();
				ch.is_busy = true;          // set character.is_busy true

				//move player to player's preparing_position
				ch.target = positions[0];
				ch.target_reached = false;
				while (!ch.target_reached)
					yield return null;

				//resolve rotations
				Vector3 direction = new Vector3(0, ch.preparing_position.eulerAngles.y, 0);
				Coroutine resolve_rotations_coroutine = StartCoroutine(ch.resolveRotations(direction));

				//remove ingredients used to prepare the dish from inventory
				inventory.removeIngredientsUsed(dish_item);

				//wait player for preparing delay
				// displpay the countdown_timer and then delete it after the countdown is over
				GameObject countdown_display = Instantiate(countdown_display_prefab, positions[0] + countdown_display_position_offset, Quaternion.Euler(new Vector3(45, 0, 0)));
				countdown_display.GetComponentInChildren<CountdownDisplay>().setTimer(dish_item.time_to_prepare);
				Destroy(countdown_display, dish_item.time_to_prepare);
				yield return new WaitForSeconds(dish_item.time_to_prepare);

				//stop resolve_rotations_on_return
				if (resolve_rotations_coroutine != null)
					StopCoroutine(resolve_rotations_coroutine);

				//add item to inventory
				bool has_added = inventory.addItem(dish_item);
				if (has_added)
				{
					Debug.Log(dish_item.name + " added to inventory");
				}
				else
				{
					Debug.Log("can not add " + dish_item.name + " to inventory");
					Test_script2.ts2.applyText("can not add " + dish_item.name + " to inventory");
				}

				//move player to starting position
				ch.target = positions[1];
				ch.target_reached = false;
				while (!ch.target_reached)
					yield return null;
				Debug.Log("player returns");

				//resolve rotations
				direction = new Vector3(0, ch.starting_rotation.y, 0);
				Coroutine resolve_rotations_on_return = StartCoroutine(ch.resolveRotations(direction));
				yield return new WaitForSeconds(1);
				//stop resolve_rotations_on_return
				if (resolve_rotations_on_return != null)
					StopCoroutine(resolve_rotations_on_return);

				//set chef not busy
				ch.is_busy = false;
			}
		}
	}

	void default_method()
	{
		Debug.Log("Invalid input command!");
	}
}
