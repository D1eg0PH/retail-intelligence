import {
    LayoutDashboard,
    FileUp,
    ShoppingCart,
    Barcode,
    Settings,
    Store,
    PackageSearch,
    ClipboardList,
    Wallet
} from "lucide-react";

import { NavLink } from "react-router-dom";

const menu = [
    {
        name: "Gastos y ganancias",
        path: "/dashboard/eleventa/finanzas",
        icon: Wallet
    },
    {
        name: "Importaciones",
        path: "/",
        icon: FileUp
    },
    {
        name: "Pedidos",
        path: "/pedidos",
        icon: ShoppingCart
    },
    {
        name: "Códigos",
        path: "/barcodes",
        icon: Barcode
    },
    {
        name: "Dashboard",
        path: "/dashboard",
        icon: LayoutDashboard
    },
    {
        name: "Dashboard Eleventa",
        path: "/dashboard/eleventa",
        icon: Store
    },
    {
        name: "Depurador Eleventa",
        path: "/dashboard/eleventa/depurador",
        icon: PackageSearch
    },
    {
        name: "Pedidos Eleventa",
        path: "/dashboard/eleventa/pedidos",
        icon: ClipboardList
    },
    {
        name: "Configuración",
        path: "/configuracion",
        icon: Settings
    }
];

export default function Sidebar() {
    return (
        <aside className="w-64 bg-slate-900 text-white flex flex-col">

            <div className="p-6 border-b border-slate-800">

                <h1 className="text-2xl font-bold">
                    Retail Intelligence
                </h1>

                <p className="text-sm text-slate-400 mt-1">
                    Sistema de gestión
                </p>

            </div>

            <nav className="flex-1 p-4 space-y-2">

                {menu.map((item) => {
                    const Icon = item.icon;

                    return (
                        <NavLink
                            key={item.path}
                            to={item.path}
                            end={item.path === "/" || item.path === "/dashboard" || item.path === "/dashboard/eleventa"}
                            className={({ isActive }) =>
                                `
                                flex
                                items-center
                                gap-3
                                px-4
                                py-3
                                rounded-xl
                                transition-colors
                                ${
                                    isActive
                                        ? "bg-blue-600 text-white"
                                        : "text-slate-300 hover:bg-slate-800 hover:text-white"
                                }
                                `
                            }
                        >
                            <Icon size={20} />

                            <span>{item.name}</span>
                        </NavLink>
                    );
                })}

            </nav>

        </aside>
    );
}
