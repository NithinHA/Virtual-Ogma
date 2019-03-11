﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Customer : MonoBehaviour
{
	[Header("Time for first/ next order")]
	public float start_time;
	public float order_time;
	private float cur_time;

	float time_of_order;                    // time at which was made. Used to compute total waiting time of a customer
	int tips;								// additional score that player gets for serving food early to this customer

	Orders orders;						// singleton instance of Orders class
	Item[] dishes;						// list of all dish Items taken from KeywordsData class
	public Item dish;					//current dish

	public bool is_ordering = false;	// if is_ordering is true, then only the waiter will take order. So this condition will be used in take_order()
	public bool is_served = true;

	[Header("Materials for table top indication")]
	public Material food_order_indication_mat;
	public Material food_to_be_served_indication_mat;
	private Material default_mat;

	[Header("table_top GameObject")]
	public GameObject table_top;

    void Start()
    {
		cur_time = start_time;

		dishes = KeywordsData.instance.dish_arr;
		orders = Orders.instance;
		default_mat = table_top.GetComponent<Renderer>().material;
    }
	
    void Update()
    {
		if (is_served)
		{
			if (cur_time > 0)
			{
				cur_time -= Time.deltaTime;
			}
			else
			{
				Debug.Log("I want to order something..");
				Test_script2.ts2.applyText("I want to order something");
				is_ordering = true;
				table_top.GetComponent<Renderer>().material = food_order_indication_mat;	// make the table pink color indicating waiter has to take order from that table
				cur_time = order_time;
				is_served = false;
			}
		}
		else
		{
			// TO MAKE THINGS CHALLENGING, start a timer here and if food is served before 40s => $30 tips, 20s => $50 tips, 10s => $80 tips
			if(time_of_order == -1)				// prevents body of the if condition from executing every frame
				time_of_order = Time.time;
		}
	}

	public void order_food()		// called when player says, "TAKE ORDER FROM TABLE __"
	{
		this.dish = dishes[Random.Range(0, dishes.Length)];
		bool has_added = orders.addItem(this.dish);      // add this.dish to orders list
		if (has_added)
		{
			Debug.Log(this.dish.name + " added to orders list");
			Test_script2.ts2.applyText(this.dish.name + " added to orders list");
		}
		else
		{
			Debug.Log("can not add " + this.dish.name + " to orders list");
			Test_script2.ts2.applyText("can not add " + this.dish.name + " to orders list");
			is_ordering = true;				// so that the waiter can come back and take order some other time
			return;
		}
		Debug.Log("I'll have " + this.dish.name);
		Test_script2.ts2.applyText("I'll have " + this.dish.name);
		table_top.GetComponent<Renderer>().material = food_to_be_served_indication_mat;		// make table orange color indicating customer is waiting for the dish to be served
		
	}

	public void food_served()       // called when player says, "SERVE TABLE __"
	{
		is_served = true;
		orders.removeItem(this.dish);           // remove dish from orders list

		int serv_delay = (int)(Time.time - time_of_order);
		Debug.Log("Delay = " + serv_delay);
		Test_script2.ts2.applyText("Delay = " + serv_delay);

		if (serv_delay < 30) {
			tips = 50;
		} else if(serv_delay < 60) {
			tips = 30;
		} else if(serv_delay < 90) {
			tips = 10;
		} else {
			tips = 0;
		}
		Debug.Log("Tips received = " + tips);
		Test_script2.ts2.applyText("Tips received = " + tips);
		
		Score.instance.payBill(this.dish, tips);		// update score text

		this.dish = null;				// reset dish to null
		tips = 0;						// reset tips value to 0
		time_of_order = -1;				// reset time_of_order to -1
		// removing dish from inventory and decrementing clean utensil count is done in WaiterAction class
		table_top.GetComponent<Renderer>().material = default_mat;	// make table original color indicating customer has been served and is eating the dish
	}
}
