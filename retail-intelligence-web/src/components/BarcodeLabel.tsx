import Barcode from "react-barcode";

interface Props {
    nombre: string;
    codigoBarras: string | number;
}

export default function BarcodeLabel({
    nombre,
    codigoBarras
}: Props) {
    return (
<div
    id="barcode-label"
    style={{
        width: "113px",
        height: "57px"
    }}
    className="
        bg-white
        border
        flex
        flex-col
        items-center
        justify-start
        pt-1
    "
>
        <div
            style={{
                fontSize: "8px",
                fontWeight: "bold",
                textAlign: "center",
                width: "100%",
                marginBottom: "8px" // antes 4px
            }}
        >
            {nombre}
        </div>

    <Barcode
        value={codigoBarras.toString()}
        format="CODE128"
        width={0.7}
        height={18}
        margin={0}
        displayValue={false}
    />
</div>
    );
}