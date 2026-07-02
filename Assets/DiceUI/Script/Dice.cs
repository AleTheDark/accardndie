using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Dice : MonoBehaviour
{
    // Start is called before the first frame update
  
    public int randomNumber; //number that is chosen by a program randomly
  
    public Image image;
    public Sprite dice1;
    public Sprite dice2;
    public Sprite dice3;
    public Sprite dice4;
    public Sprite dice5;
    public Sprite dice6;
    public Sprite dice7;
    public Sprite dice8;
    public Sprite dice9;    
    public Sprite dice10;
    public Sprite dice11;
    public Sprite dice12;
    public Sprite dice13;
    public Sprite dice14;
    public Sprite dice15;
    public Sprite dice16;
    public Sprite dice17;
    public Sprite dice18;
    public Sprite dice19;
    public Sprite dice20;

    public Sprite diceStart;
    public Image diceResult;

    public Button buttonZeroAlpha;

    void Start()
    {      
      image = GetComponent<Image>();
      diceResult.GetComponent<Image>().enabled = false;
    }

    // Update is called once per frame
    void Update()
    {        
      if(Input.GetMouseButtonDown(0) && buttonZeroAlpha.gameObject.activeInHierarchy) //if mouse button is clicked does action below
      {
          randomNumber = Random.Range(1, 21);   //range of numbers on the dice is 1-20
          //randomNumber2 = randomNumber.ToString(); // number is converted to the text form
         // result.text = randomNumber2; // number is shown in canvas => dice => textresult
         
         if(randomNumber == 1)
         {           
              StartCoroutine(ShowAfter1());    
         }
         else if(randomNumber == 2)
         {
              StartCoroutine(ShowAfter2());              
         }
         else if(randomNumber == 3)
         {
              StartCoroutine(ShowAfter3());              
         }
         else if(randomNumber == 4)
         {
              StartCoroutine(ShowAfter4());              
         }
         else if(randomNumber == 5)
         {
              StartCoroutine(ShowAfter5());              
         }
         else if(randomNumber == 6)
         {
              StartCoroutine(ShowAfter6());              
         }
         else if(randomNumber == 7)
         {
              StartCoroutine(ShowAfter7());              
         }
         else if(randomNumber == 8)
         {
              StartCoroutine(ShowAfter8());              
         }
         else if(randomNumber == 9)
         {
              StartCoroutine(ShowAfter9());              
         }
         else if(randomNumber == 10)
         {
              StartCoroutine(ShowAfter10());              
         }
         else if(randomNumber == 11)
         {
              StartCoroutine(ShowAfter11());              
         }
         else if(randomNumber == 12)
         {
              StartCoroutine(ShowAfter12());              
         }
         else if(randomNumber == 13)
         {
              StartCoroutine(ShowAfter13());              
         }
         else if(randomNumber == 14)
         {
              StartCoroutine(ShowAfter14());              
         }
         else if(randomNumber == 15)
         {
              StartCoroutine(ShowAfter15());              
         }
         else if(randomNumber == 16)
         {
              StartCoroutine(ShowAfter16());              
         }
         else if(randomNumber == 17)
         {
              StartCoroutine(ShowAfter17());              
         }
         else if(randomNumber == 18)
         {
              StartCoroutine(ShowAfter18());              
         }
         else if(randomNumber == 19)
         {
              StartCoroutine(ShowAfter19());              
         }
         else if(randomNumber == 20)
         {
              StartCoroutine(ShowAfter20());              
         }

         if(Input.GetMouseButtonDown(0) && buttonZeroAlpha.gameObject.activeInHierarchy) 
         {             
             diceResult.GetComponent<Image>().enabled = false;
         }
                   
      }
    }

    IEnumerator ShowAfter1() //controls apprarance of the correct dice number 
    {    
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice1;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
        
    }

    IEnumerator ShowAfter2()
    {        
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice2;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter3()
    {      
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice3;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter4()
    {        
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice4;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter5()
    {  
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice5;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter6()
    {      
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice6;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter7()
    {       
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice7;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter8()
    {        
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice8;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter9()
    {       
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice9;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter10()
    {
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice10;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter11()
    {        
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice11;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter12()
    {        
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice12;
       yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter13()
    {   
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice13;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }
    
    IEnumerator ShowAfter14()
    {    
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice14;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter15()
    {        
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice15;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter16()
    {  
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice16;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter17()
    {    
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice17;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter18()
    {        
       yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice18;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter19()
    {        
       yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice19;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }

    IEnumerator ShowAfter20()
    {        
        yield return new WaitForSeconds(0.2f);
        buttonZeroAlpha.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        diceResult.GetComponent<Image>().enabled = true;
        gameObject.GetComponent<Image>().sprite = dice20;
        yield return new WaitForSeconds(3f);                
        buttonZeroAlpha.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        gameObject.GetComponent<Image>().sprite = diceStart;
        diceResult.GetComponent<Image>().enabled = false;
    }
}
