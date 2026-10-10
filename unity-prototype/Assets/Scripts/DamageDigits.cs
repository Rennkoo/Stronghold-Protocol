public static class DamageDigits {
    public static long Magnitude(int value) {return value<0?-(long)value:value;}
    public static int Count(long value) {int count=1;while(value>=10){value/=10;count++;}return count;}
    public static long Divisor(int count) {long divisor=1;for(int i=1;i<count;i++)divisor*=10;return divisor;}
    public static ulong Pack(long value) {ulong result=0;int shift=0;do{result|=(ulong)(value%10)<<shift;shift+=4;value/=10;}while(value>0);return result;}
}
