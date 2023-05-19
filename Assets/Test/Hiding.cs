using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hiding : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Humanoid h = new Humanoid();
        Enemy e = new Enemy();
        Orc o = new Orc();
        NPC n = new NPC();
        Seller s = new Seller();

        h.Yell();
        e.Yell();
        o.Yell();
        n.Yell();
        s.Yell();

        Debug.Log("Up Casting");
        Humanoid he = (Humanoid)e;
        Enemy eo = (Enemy)o;
        he.Yell();
        eo.Yell();

        Humanoid hn = (Humanoid)n;
        NPC ns = (NPC)s;
        hn.Yell();
        ns.Yell();

        Debug.Log("Down Casting");
        Humanoid oh = new Orc();
        Enemy oe = new Orc();
        oh.Yell();
        oe.Yell();

        Humanoid nh = new NPC();
        Humanoid sh = new Seller();
        nh.Yell();
        sh.Yell();

    }
}

public class Humanoid
{
    public virtual void Yell()
    {
        Debug.Log("Humanoid yell");
    }
}

public class Enemy : Humanoid
{
    public override void Yell()
    {
        base.Yell();
        Debug.Log("Enemy yell");
    }
}

public class NPC : Humanoid
{
    public new void Yell()
    {
        base.Yell();
        Debug.Log("NPC Yell");
    }
}

public class Seller : NPC
{
    public new void Yell()
    {
        base.Yell();
        Debug.Log("Seller Yell");
    }
}

public class Orc : Enemy
{
    public new void Yell()
    {
        base.Yell();
        Debug.Log("Orc yell");
    }
}